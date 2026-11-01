Unicode true
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

Name "J.A.R.V.I.S."
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\JARVIS"
InstallDirRegKey HKCU "Software\JARVIS" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "J.A.R.V.I.S."
VIAddVersionKey "FileDescription" "J.A.R.V.I.S. desktop assistant setup"
VIAddVersionKey "FileVersion" "1.0.0"
VIAddVersionKey "LegalCopyright" "J.A.R.V.I.S. contributors"

Var GgufPath
Var GgufInput
Var GgufName
Var Dialog

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "Install J.A.R.V.I.S. for your Windows account.$\r$\n$\r$\nUse cloud providers without a local model, or optionally import a GGUF file during setup.$\r$\n$\r$\nThe .NET runtime is included. Close any installed copy of J.A.R.V.I.S. before continuing."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
Page custom GgufPage GgufPageLeave
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\JARVIS.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch J.A.R.V.I.S."
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "This installer requires 64-bit Windows."
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "J.A.R.V.I.S. requires Windows 10 or newer."
    Abort
  ${EndIf}
  SetShellVarContext current
FunctionEnd

Function GgufPage
  !insertmacro MUI_HEADER_TEXT "Optional local model" "Choose a GGUF you already downloaded, or leave this empty."
  nsDialogs::Create 1018
  Pop $Dialog
  ${NSD_CreateLabel} 0 0 100% 40u "Cloud-only? Leave this empty and click Install.$\r$\nFor local AI, select a complete llama.cpp-compatible GGUF chat/instruct file. It will be copied into the installed models folder. Allow enough free disk space for the copy."
  Pop $0
  ${NSD_CreateFileRequest} 0 48u 78% 14u "$GgufPath"
  Pop $GgufInput
  ${NSD_CreateBrowseButton} 80% 48u 20% 14u "Browse..."
  Pop $0
  ${NSD_OnClick} $0 BrowseGguf
  ${NSD_CreateLabel} 0 78u 100% 55u "You can add models later under Settings > Engine > Import GGUF.$\r$\n$\r$\nLocal inference uses CPU and needs RAM for model weights plus context. ZIP archives and safetensors cannot be imported here. Existing models and saved chats are preserved when uninstalling."
  Pop $0
  nsDialogs::Show
FunctionEnd

Function BrowseGguf
  nsDialogs::SelectFileDialog open "" "GGUF models (*.gguf)|*.gguf"
  Pop $0
  ${If} $0 != ""
    ${NSD_SetText} $GgufInput $0
  ${EndIf}
FunctionEnd

Function GgufPageLeave
  ${NSD_GetText} $GgufInput $GgufPath
  ${If} $GgufPath == ""
    Return
  ${EndIf}
  IfFileExists "$GgufPath" +3 0
    MessageBox MB_ICONEXCLAMATION "Select an existing GGUF file, or clear the field to skip."
    Abort
  ${GetFileExt} "$GgufPath" $0
  ${If} $0 != "gguf"
    MessageBox MB_ICONEXCLAMATION "Choose a .gguf model file."
    Abort
  ${EndIf}
  FileOpen $0 "$GgufPath" r
  FileRead $0 $1 4
  FileClose $0
  ${If} $1 != "GGUF"
    MessageBox MB_ICONEXCLAMATION "This file does not have a valid GGUF header. Download a complete model file."
    Abort
  ${EndIf}
FunctionEnd

Section "J.A.R.V.I.S. application (required)" SEC_APP
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "${PAYLOAD_DIR}\*.*"
  CreateDirectory "$INSTDIR\models"
  CreateDirectory "$SMPROGRAMS\J.A.R.V.I.S."
  CreateShortcut "$SMPROGRAMS\J.A.R.V.I.S.\J.A.R.V.I.S..lnk" "$INSTDIR\JARVIS.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortcut "$SMPROGRAMS\J.A.R.V.I.S.\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\JARVIS" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS" "DisplayName" "J.A.R.V.I.S."
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS" "DisplayVersion" "1.0.0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS" "DisplayIcon" "$INSTDIR\JARVIS.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS" "NoRepair" 1
SectionEnd

Section "Desktop shortcut" SEC_DESKTOP
  CreateShortcut "$DESKTOP\J.A.R.V.I.S..lnk" "$INSTDIR\JARVIS.exe"
SectionEnd

!ifdef BUNDLED_GGUF
Section /o "Bundled local model: ${BUNDLED_NAME}" SEC_MODEL
  SetOutPath "$INSTDIR\models"
  SetOverwrite off
  File "${BUNDLED_GGUF}"
  SetOverwrite on
SectionEnd
!endif

Section -ImportGguf
  ${If} $GgufPath != ""
    ${GetFileName} "$GgufPath" $GgufName
    IfFileExists "$INSTDIR\models\$GgufName" model_exists 0
    ClearErrors
    CopyFiles /SILENT "$GgufPath" "$INSTDIR\models\$GgufName"
    ${If} ${Errors}
      MessageBox MB_ICONEXCLAMATION "The app installed, but the model could not be copied. Use Settings > Engine > Import GGUF after freeing disk space."
    ${EndIf}
    Goto model_done
    model_exists:
      DetailPrint "Preserved existing model: $GgufName"
    model_done:
  ${EndIf}
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  !include "${UNINSTALL_FILES}"
  Delete "$DESKTOP\J.A.R.V.I.S..lnk"
  Delete "$SMPROGRAMS\J.A.R.V.I.S.\J.A.R.V.I.S..lnk"
  Delete "$SMPROGRAMS\J.A.R.V.I.S.\Uninstall.lnk"
  RMDir "$SMPROGRAMS\J.A.R.V.I.S."
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR\models"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\JARVIS"
  DeleteRegKey HKCU "Software\JARVIS"
SectionEnd
