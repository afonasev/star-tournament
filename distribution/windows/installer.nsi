# -*- coding: utf-8 -*-
Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 32

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "nsDialogs.nsh"

!ifndef INPUT_DIR
  !error "INPUT_DIR must point to the extracted Velopack Portable ZIP layout"
!endif
!ifndef VERSION
  !error "VERSION must be a semantic version"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE must be the setup executable path"
!endif

!define PRODUCT "Star Tournament"
!define PRODUCT_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\StarTournament.Native"
!define MARKER_NAME ".star-tournament-install.json"
!define BROKER "$BrokerDir\StarTournamentMaintenance.exe"

Name "${PRODUCT} ${VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$PROGRAMFILES64\Star Tournament"
InstallDirRegKey HKLM "${PRODUCT_KEY}" "InstallLocation"
ShowInstDetails show
ShowUninstDetails show
; Modern UI applies its icon settings when the first page is inserted.
!define MUI_ICON "${INPUT_DIR}\app-icon.ico"
!define MUI_UNICON "${INPUT_DIR}\app-icon.ico"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchInstalledGame
!define MUI_FINISHPAGE_RUN_TEXT "Запустить Star Tournament"
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\Star Tournament.exe"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Создать ярлык на рабочем столе"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION CreateDesktopShortcut
!define MUI_WELCOMEPAGE_TITLE "Установка Star Tournament"
!define MUI_WELCOMEPAGE_TEXT "Мастер установит Star Tournament для всех пользователей компьютера."
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_VERIFYONLEAVE
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE ValidateInstallTarget
!insertmacro MUI_PAGE_DIRECTORY
Page custom ExistingInstallPage
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Var BrokerOutput
Var BrokerStatus
Var FindHandle
Var FindName
Var HasMarker
Var TargetKnown
Var InstallFlag
Var BrokerDir
Var StageDir
Var InstallMode
Var ExistingVersion
Var TransactionActive
Var TransactionRoot
Var ConfirmDialog
Var InstallerPid

Section "Star Tournament (обязательный компонент)" MainInstall
  SectionIn RO
  StrCpy $TransactionRoot $INSTDIR
  StrCpy $TransactionActive 1
  StrCpy $R0 '--installer begin "$INSTDIR" "${VERSION}" $InstallerPid'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    Goto install_transaction_failed
  ${EndIf}
  ${If} $BrokerOutput != "$InstallMode|$ExistingVersion"
    MessageBox MB_ICONEXCLAMATION|MB_OK "Состояние установки изменилось. Вернитесь к выбору папки и проверьте установленную версию ещё раз."
    Goto install_transaction_failed
  ${EndIf}
  StrCpy $StageDir "$INSTDIR.installer\stage"

  ; Every file goes into the protected sibling stage. The previous root is untouched.
  SetOutPath "$StageDir"
  ClearErrors
  File /oname=.portable "${INPUT_DIR}\.portable"
  File /r "${INPUT_DIR}\*"
  IfErrors install_transaction_failed
  ClearErrors
  FileOpen $InstallFlag "$StageDir\${MARKER_NAME}" w
  IfErrors install_transaction_failed
  FileWrite $InstallFlag "{$\"protocol$\":1,$\"product$\":$\"Star Tournament$\",$\"installerVersion$\":$\"${VERSION}$\"}$\r$\n"
  FileClose $InstallFlag
  IfErrors install_transaction_failed
  ClearErrors
  FileOpen $InstallFlag "$StageDir\maintenance\installing.flag" w
  IfErrors install_transaction_failed
  FileWrite $InstallFlag "${VERSION}$\r$\n"
  FileClose $InstallFlag
  IfErrors install_transaction_failed
  ; Keep INSTDIR bound to the final root; the generated uninstaller moves with stage.
  ClearErrors
  WriteUninstaller "$StageDir\Uninstall.exe"
  IfErrors install_transaction_failed

  SetOutPath "$BrokerDir"
  StrCpy $R0 '--installer commit "$INSTDIR" "${VERSION}" $InstallerPid'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    Goto install_transaction_failed
  ${EndIf}
  StrCpy $R0 '--installer finalize "$INSTDIR" "${VERSION}" $InstallerPid'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    Goto install_transaction_failed
  ${EndIf}
  StrCpy $TransactionActive 0

  ; Ordinary shortcuts are created only after the transaction commit point.
  ClearErrors
  CreateDirectory "$SMPROGRAMS\Star Tournament"
  CreateShortcut "$SMPROGRAMS\Star Tournament\Star Tournament.lnk" "$INSTDIR\Star Tournament.exe" "" "$INSTDIR\app-icon.ico" 0
  CreateShortcut "$SMPROGRAMS\Star Tournament\Uninstall Star Tournament.lnk" "$INSTDIR\Uninstall.exe"
  ${If} ${Errors}
    MessageBox MB_ICONEXCLAMATION|MB_OK "Игра установлена, но не удалось создать ярлыки в меню «Пуск». Повторите восстановление или откройте Star Tournament.exe в папке установки."
  ${EndIf}
  ; The explicit finish option controls only this product's desktop shortcut.
  Delete "$DESKTOP\Star Tournament.lnk"
  Goto install_done

  install_transaction_failed:
    DetailPrint "$BrokerOutput"
    Call RestoreInterruptedInstall
    SetErrorLevel 1
    Abort "Установка не завершена. Прежние файлы сохранены или восстановлены; если восстановление не удалось, закройте игру и повторите этот установщик."
  install_done:
SectionEnd

Function RestoreInterruptedInstall
  ${If} $TransactionActive == 1
    SetOutPath "$BrokerDir"
    StrCpy $R0 '--installer resume "$TransactionRoot" "${VERSION}" $InstallerPid'
    Call CallBroker
    ${If} $BrokerStatus == "0"
      StrCpy $TransactionActive 0
    ${Else}
      DetailPrint "Не удалось завершить восстановление: $BrokerOutput"
    ${EndIf}
  ${EndIf}
FunctionEnd

Function .onGUIEnd
  Call RestoreInterruptedInstall
  SetOutPath "$TEMP"
  ${If} $BrokerDir != ""
    Delete "$BrokerDir"
    ClearErrors
    RMDir /r "$BrokerDir"
  ${EndIf}
FunctionEnd

Function ExistingInstallPage
  ${If} $InstallMode == "fresh"
    Abort
  ${EndIf}
  !insertmacro MUI_HEADER_TEXT "Обнаружена установленная игра" "Файлы приложения будут восстановлены или обновлены."
  nsDialogs::Create 1018
  Pop $ConfirmDialog
  ${If} $ConfirmDialog == error
    MessageBox MB_ICONSTOP|MB_OK "Не удалось открыть подтверждение восстановления. Файлы игры не изменены."
    Quit
  ${EndIf}
  ${NSD_CreateLabel} 0 0 100% 30u "$INSTDIR"
  Pop $0
  ${NSD_CreateLabel} 0 36u 100% 16u "Установлена версия: $ExistingVersion"
  Pop $0
  ${NSD_CreateLabel} 0 58u 100% 16u "Версия установщика: ${VERSION}"
  Pop $0
  ${If} $InstallMode == "repair"
    ${NSD_CreateLabel} 0 82u 100% 24u "Установлена та же версия. Восстановить файлы приложения."
    Pop $0
    GetDlgItem $0 $HWNDPARENT 1
    SendMessage $0 ${WM_SETTEXT} 0 "STR:Восстановить"
  ${Else}
    ${NSD_CreateLabel} 0 82u 100% 24u "Обновить файлы приложения до новой версии."
    Pop $0
    GetDlgItem $0 $HWNDPARENT 1
    SendMessage $0 ${WM_SETTEXT} 0 "STR:Обновить"
  ${EndIf}
  ${NSD_CreateLabel} 0 112u 100% 20u "Настройки, профили и история сохранятся."
  Pop $0
  nsDialogs::Show
FunctionEnd

Function CreateDesktopShortcut
  SetShellVarContext all
  ClearErrors
  CreateDirectory "$DESKTOP"
  IfErrors desktop_shortcut_failed
  CreateShortcut "$DESKTOP\Star Tournament.lnk" "$INSTDIR\Star Tournament.exe" "" "$INSTDIR\app-icon.ico" 0
  IfErrors desktop_shortcut_failed
  IfFileExists "$DESKTOP\Star Tournament.lnk" desktop_shortcut_done desktop_shortcut_failed
  desktop_shortcut_failed:
    DetailPrint "Не удалось создать ярлык: $DESKTOP\Star Tournament.lnk"
    MessageBox MB_ICONEXCLAMATION|MB_OK "Игра установлена, но Windows не удалось создать ярлык на рабочем столе.$\r$\nПуть: $DESKTOP\Star Tournament.lnk$\r$\nИгру можно открыть из меню «Пуск»."
  desktop_shortcut_done:
FunctionEnd

Function .onInit
  ${IfNot} ${AtLeastWin10}
  ${OrIfNot} ${AtLeastBuild} 19043
    MessageBox MB_ICONSTOP|MB_OK "Для Star Tournament требуется Windows 10 21H1 (сборка 19043) или новее."
    Abort
  ${EndIf}
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "Для установки Star Tournament требуется 64-разрядная версия Windows."
    Abort
  ${EndIf}
  SetRegView 64
  SetShellVarContext all
  System::Call 'kernel32::GetCurrentProcessId() i .r0'
  StrCpy $InstallerPid $0
  ReadRegStr $0 HKLM "${PRODUCT_KEY}" "InstallLocation"
  ${If} $0 != ""
    StrCpy $INSTDIR $0
  ${EndIf}
  Call PrepareProtectedBroker

  StrCpy $R0 '--check-legacy-user'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    MessageBox MB_ICONEXCLAMATION|MB_OK "Не удалось безопасно подтвердить отсутствие пользовательской установки Star Tournament. Удалите старую копию из той же учётной записи Windows. Если установщик запущен с учётными данными другой учётной записи администратора, повторите запуск из исходной учётной записи."
    Abort
  ${EndIf}
FunctionEnd

!macro ProtectedBroker PREFIX
Function ${PREFIX}PrepareProtectedBroker
  ClearErrors
  GetTempFileName $BrokerDir "$PROGRAMFILES64"
  IfErrors broker_custody_failed
  Delete "$BrokerDir"
  CreateDirectory "$BrokerDir"
  IfErrors broker_custody_failed
  ; Mark ownership before extracting a large runtime, so a later run can clean
  ; this exact protected directory after a killed installer.
  System::Call 'kernel32::GetCurrentProcessId() i .r0'
  StrCpy $InstallerPid $0
  FileOpen $InstallFlag "$BrokerDir\.star-tournament-installer-temp.json" w
  IfErrors broker_custody_failed
  FileWrite $InstallFlag "{$\"schema$\":1,$\"product$\":$\"Star Tournament installer$\",$\"pid$\":$InstallerPid}$\r$\n"
  FileClose $InstallFlag
  IfErrors broker_custody_failed
  SetOutPath "$BrokerDir"
  File /r "${INPUT_DIR}\maintenance\*"
  IfErrors broker_custody_failed
  Return
  broker_custody_failed:
    MessageBox MB_ICONSTOP|MB_OK "Не удалось подготовить защищённый компонент установки в Program Files. Файлы игры не изменены."
    Abort
FunctionEnd
!macroend
!insertmacro ProtectedBroker ""
!insertmacro ProtectedBroker "un."

Function un.onGUIEnd
  SetOutPath "$TEMP"
  ${If} $BrokerDir != ""
    Delete "$BrokerDir"
    ClearErrors
    RMDir /r "$BrokerDir"
  ${EndIf}
FunctionEnd

Function CallBroker
  ; Caller puts the complete broker arguments in $R0.
  nsExec::ExecToStack '"${BROKER}" $R0'
  Pop $BrokerStatus
  Pop $BrokerOutput
FunctionEnd

Function ValidateInstallTarget
  GetInstDirError $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP|MB_OK "Недостаточно свободного места или папка установки недоступна. Освободите место и повторите попытку."
    Abort
  ${EndIf}
  StrCpy $R0 '--installer resume "$INSTDIR" "${VERSION}" $InstallerPid'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    MessageBox MB_ICONSTOP|MB_OK "Не удалось завершить предыдущую установку. Закройте игру и повторите попытку.$\r$\n$BrokerOutput"
    Abort
  ${EndIf}
  StrCpy $R0 '--validate-root "$INSTDIR"'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    MessageBox MB_ICONSTOP|MB_OK "Эту папку нельзя использовать для установки. Выберите папку приложения вне корня диска и системных папок Windows. Возможно, папка или один из родительских каталогов является ссылкой.$\r$\n$BrokerOutput"
    Abort
  ${EndIf}

  StrCpy $HasMarker 0
  IfFileExists "$INSTDIR\${MARKER_NAME}" 0 +2
    StrCpy $HasMarker 1

  ; Existing roots may contain only paths owned by this installer.
  StrCpy $TargetKnown 1
  FindFirst $FindHandle $FindName "$INSTDIR\*.*"
  find_target_entry:
    StrCmp $FindName "" find_target_done
    StrCmp $FindName "." find_target_next
    StrCmp $FindName ".." find_target_next
    StrCmp $FindName "${MARKER_NAME}" find_target_next
    StrCmp $FindName ".portable" find_target_next
    StrCmp $FindName "app-icon.ico" find_target_next
    StrCmp $FindName "Star Tournament.exe" find_target_next
    StrCmp $FindName "Update.exe" find_target_next
    StrCmp $FindName "Uninstall.exe" find_target_next
    StrCmp $FindName "current" find_target_next
    StrCmp $FindName "packages" find_target_next
    StrCmp $FindName "maintenance" find_target_next
    StrCpy $TargetKnown 0
    Goto find_target_next
  find_target_next:
    FindNext $FindHandle $FindName
    Goto find_target_entry
  find_target_done:
    FindClose $FindHandle
  ${If} $TargetKnown == 0
    MessageBox MB_ICONSTOP|MB_OK "В выбранной папке есть файлы, которые не принадлежат Star Tournament. Выберите пустую папку или каталог существующей установки."
    Abort
  ${EndIf}
  ${If} $HasMarker == 0
    IfFileExists "$INSTDIR\*.*" 0 target_is_empty
    MessageBox MB_ICONSTOP|MB_OK "В выбранной папке есть файлы без маркера Star Tournament. Выберите пустую папку."
    Abort
  target_is_empty:
  ${EndIf}

  StrCpy $R0 '--check-idle "$INSTDIR"'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    MessageBox MB_ICONSTOP|MB_OK "Star Tournament запущена или установщик не смог проверить, что игра закрыта. Закройте игру и повторите попытку.$\r$\n$BrokerOutput"
    Abort
  ${EndIf}
  StrCpy $R0 '--check-install "$INSTDIR" "${VERSION}"'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    MessageBox MB_ICONSTOP|MB_OK "Нельзя безопасно заменить эту установку. Проверьте версию, канал и папку установки.$\r$\n$BrokerOutput"
    Abort
  ${EndIf}
  StrCpy $InstallMode $BrokerOutput
  StrCpy $R0 '--installed-version "$INSTDIR" "${VERSION}"'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    MessageBox MB_ICONSTOP|MB_OK "Не удалось прочитать установленную версию.$\r$\n$BrokerOutput"
    Abort
  ${EndIf}
  StrCpy $ExistingVersion $BrokerOutput

FunctionEnd

Function LaunchInstalledGame
  ExecWait '"$INSTDIR\maintenance\StarTournamentMaintenance.exe" --launch-user "$INSTDIR\Star Tournament.exe"' $0
  ${If} $0 != 0
    MessageBox MB_ICONEXCLAMATION|MB_OK "Star Tournament установлена, но Windows не удалось запустить её от имени вошедшего пользователя. Запустите игру из меню «Пуск»."
  ${EndIf}
FunctionEnd

Function un.onInit
  SetRegView 64
  SetShellVarContext all
  ; NSIS initializes $INSTDIR to the original uninstaller's installation folder,
  ; then runs a temporary executable copy. $EXEDIR points at that temporary copy.
  ; Preserve the original root, including partial installs without registration.
  ReadRegStr $0 HKLM "${PRODUCT_KEY}" "InstallLocation"
  StrCmp $0 "" un_registry_path_ok
  StrCmp $0 "$INSTDIR" un_registry_path_ok
  MessageBox MB_ICONSTOP|MB_OK "Путь в системном реестре не совпадает с папкой этого деинсталлятора. Файлы не будут удалены."
  Abort
  un_registry_path_ok:
  Call un.PrepareProtectedBroker
  ExecWait '"${BROKER}" --validate-root "$INSTDIR"' $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP|MB_OK "Папка не прошла проверку владения Star Tournament. Файлы не будут удалены."
    Abort
  ${EndIf}
FunctionEnd

Section "Uninstall"
  ; Prevent new launches before the final process check and owned-file removal.
  ; ReadRegStr in un.onInit may set the error flag for a partial installation.
  ClearErrors
  FileOpen $InstallFlag "$INSTDIR\maintenance\installing.flag" w
  IfErrors un_lock_failed
  FileWrite $InstallFlag "uninstall$\r$\n"
  FileClose $InstallFlag

  ExecWait '"${BROKER}" --check-idle "$INSTDIR"' $0
  ${If} $0 != 0
    Delete "$INSTDIR\maintenance\installing.flag"
    SetErrorLevel 1
    Abort "Закройте Star Tournament перед удалением."
  ${EndIf}
  ExecWait '"${BROKER}" --validate-root "$INSTDIR"' $0
  ${If} $0 != 0
    Delete "$INSTDIR\maintenance\installing.flag"
    SetErrorLevel 1
    Abort "Папка не прошла проверку владения Star Tournament. Файлы не удалены."
  ${EndIf}

  Delete "$DESKTOP\Star Tournament.lnk"
  Delete "$SMPROGRAMS\Star Tournament\Star Tournament.lnk"
  Delete "$SMPROGRAMS\Star Tournament\Uninstall Star Tournament.lnk"
  RMDir "$SMPROGRAMS\Star Tournament"
  DeleteRegKey HKLM "${PRODUCT_KEY}"

  Delete "$INSTDIR\app-icon.ico"
  Delete "$INSTDIR\.portable"
  RMDir /r "$INSTDIR\current"
  RMDir /r "$INSTDIR\packages"
  RMDir /r "$INSTDIR\maintenance"
  Delete "$INSTDIR\Star Tournament.exe"
  Delete "$INSTDIR\Update.exe"
  Delete "$INSTDIR\${MARKER_NAME}"
  Delete /REBOOTOK "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Goto un_done

  un_lock_failed:
    SetErrorLevel 1
    Abort "Не удалось создать блокировку удаления."
  un_done:
SectionEnd
