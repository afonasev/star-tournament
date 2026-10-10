# Affected checks selected before launch
Baseline69df479144dbefa5d525733562368d8236bd3b2c. Asset-only: offline tempo1.2 in generator,20 derived MP3 and previews/provenance. Raw, pitch ratio/radio chain, C# runtime/catalog/meta/prefs/scheduler unchanged.

- Existing generator completion + FFprobe/loudness/hashes:20 raw hashes unchanged, mono48k and duration approximately old/1.2 (encoding tolerance60ms).
- `unity/tools.sh test-edit-filter BotBanterVoiceCatalogTests.EveryImportedRecordingIsNonemptyMonoWithBoundedDuration`: Unity audio import all20.
- `unity/tools.sh test-play-filter BotReactionSettingsTests.EveryPhrasePlaysItsOwnClipFromOneSource`: actual20 AudioSource selections/playback, single source and unknown/invalid selection unchanged.

No full/Player/build/release/deploy; no settings/AI/lifecycle code changed, so their already passing contracts need no repeat. Listening remains human acceptance. Old bank evidence is revision-bound; faster comparison and new XML/hash are here, current previews remain bot-banter-full-voice.
