# -*- coding: utf-8 -*-
Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 32

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

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
!define BROKER "$PLUGINSDIR\StarTournamentMaintenance.exe"

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
!define MUI_PAGE_CUSTOMFUNCTION_LEAVE ValidateInstallTarget
!insertmacro MUI_PAGE_DIRECTORY
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

Section "Star Tournament (обязательный компонент)" MainInstall
  SectionIn RO
  ; Validate before creating any files. The lock then closes the launch race.
  StrCpy $R0 '--validate-root "$INSTDIR"'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    SetErrorLevel 1
    Abort "Не удалось проверить папку установки."
  ${EndIf}

  ; Directory validation ends FindFirst/FindNext with the NSIS error flag set.
  ; Start a fresh filesystem check so a successful lock is not treated as failure.
  ClearErrors
  CreateDirectory "$INSTDIR\maintenance"
  IfErrors install_lock_failed
  FileOpen $InstallFlag "$INSTDIR\maintenance\installing.flag" w
  IfErrors install_lock_failed
  FileWrite $InstallFlag "${VERSION}$\r$\n"
  FileClose $InstallFlag

  StrCpy $R0 '--check-idle "$INSTDIR"'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    Delete "$INSTDIR\maintenance\installing.flag"
    SetErrorLevel 1
    Abort "Закройте Star Tournament перед установкой."
  ${EndIf}
  StrCpy $R0 '--check-install "$INSTDIR" "${VERSION}"'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    Delete "$INSTDIR\maintenance\installing.flag"
    SetErrorLevel 1
    Abort "Установленную версию нельзя безопасно заменить."
  ${EndIf}

  ; Record ownership and write the uninstaller before payload files, so a partial
  ; first install can still be removed. The uninstaller embeds its own broker.
  ClearErrors
  FileOpen $InstallFlag "$INSTDIR\${MARKER_NAME}" w
  IfErrors install_recovery_setup_failed
  FileWrite $InstallFlag "{$\"protocol$\":1,$\"product$\":$\"Star Tournament$\",$\"installerVersion$\":$\"${VERSION}$\"}$\r$\n"
  FileClose $InstallFlag
  ClearErrors
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  IfErrors install_recovery_setup_failed

  ; Put the broker in place before the game payload. Its runtime DLLs are copied too.
  SetOutPath "$INSTDIR\maintenance"
  ClearErrors
  File /r "${INPUT_DIR}\maintenance\*"
  IfErrors install_payload_failed

  ; The .portable marker is a zero-byte file in the Velopack Portable ZIP.
  SetOutPath "$INSTDIR"
  ClearErrors
  File /oname=.portable "${INPUT_DIR}\.portable"
  File /r "${INPUT_DIR}\*"
  IfErrors install_payload_failed

  WriteRegStr HKLM "${PRODUCT_KEY}" "DisplayName" "${PRODUCT}"
  WriteRegStr HKLM "${PRODUCT_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${PRODUCT_KEY}" "Publisher" "Star Tournament"
  WriteRegStr HKLM "${PRODUCT_KEY}" "DisplayIcon" "$INSTDIR\app-icon.ico"
  WriteRegStr HKLM "${PRODUCT_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${PRODUCT_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${PRODUCT_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${PRODUCT_KEY}" "NoRepair" 1

  CreateDirectory "$SMPROGRAMS\Star Tournament"
  CreateShortcut "$SMPROGRAMS\Star Tournament\Star Tournament.lnk" "$INSTDIR\Star Tournament.exe" "" "$INSTDIR\app-icon.ico" 0
  CreateShortcut "$SMPROGRAMS\Star Tournament\Uninstall Star Tournament.lnk" "$INSTDIR\Uninstall.exe"
  ; A repair with the desktop option unchecked removes only our prior shortcut.
  Delete "$DESKTOP\Star Tournament.lnk"
  Delete "$INSTDIR\maintenance\installing.flag"
  Goto install_done

  install_lock_failed:
    RMDir "$INSTDIR\maintenance"
    SetErrorLevel 1
    Abort "Не удалось создать блокировку установки."
  install_recovery_setup_failed:
    Delete "$INSTDIR\${MARKER_NAME}"
    Delete "$INSTDIR\maintenance\installing.flag"
    RMDir "$INSTDIR\maintenance"
    SetErrorLevel 1
    Abort "Не удалось подготовить безопасное восстановление установки."
  install_payload_failed:
    Delete "$INSTDIR\maintenance\installing.flag"
    SetErrorLevel 1
    Abort "Windows не удалось скопировать файлы установки."
  install_done:
SectionEnd

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
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "Для установки Star Tournament требуется 64-разрядная версия Windows."
    Abort
  ${EndIf}
  SetRegView 64
  SetShellVarContext all
  ReadRegStr $0 HKLM "${PRODUCT_KEY}" "InstallLocation"
  ${If} $0 != ""
    StrCpy $INSTDIR $0
  ${EndIf}
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  ; The broker's native runtime files must travel with it in plugin temp.
  File /r "${INPUT_DIR}\maintenance\*"

  StrCpy $R0 '--check-legacy-user'
  Call CallBroker
  ${If} $BrokerStatus != "0"
    MessageBox MB_ICONEXCLAMATION|MB_OK "Не удалось безопасно подтвердить отсутствие пользовательской установки Star Tournament. Удалите старую копию из той же учётной записи Windows. Если установщик запущен с учётными данными другой учётной записи администратора, повторите запуск из исходной учётной записи."
    Abort
  ${EndIf}
FunctionEnd

Function CallBroker
  ; Caller puts the complete broker arguments in $R0.
  nsExec::ExecToStack '"${BROKER}" $R0'
  Pop $BrokerStatus
  Pop $BrokerOutput
FunctionEnd

Function ValidateInstallTarget
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
    MessageBox MB_ICONSTOP|MB_OK "Star Tournament уже установлена. Обновляйте игру из самой игры; для полной переустановки сначала удалите существующую копию.$\r$\n$BrokerOutput"
    Abort
  ${EndIf}
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
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  ; Embed the broker so uninstall can recover a partial first install.
  File /r "${INPUT_DIR}\maintenance\*"
  ExecWait '"$PLUGINSDIR\StarTournamentMaintenance.exe" --validate-root "$INSTDIR"' $0
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

  ExecWait '"$PLUGINSDIR\StarTournamentMaintenance.exe" --check-idle "$INSTDIR"' $0
  ${If} $0 != 0
    Delete "$INSTDIR\maintenance\installing.flag"
    SetErrorLevel 1
    Abort "Закройте Star Tournament перед удалением."
  ${EndIf}
  ExecWait '"$PLUGINSDIR\StarTournamentMaintenance.exe" --validate-root "$INSTDIR"' $0
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
