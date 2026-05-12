# Run from the repository root on the gdscript-port branch.
# This removes active C# files from the Godot project after they have been copied
# as .txt reference files under docs/csharp_reference/.

$paths = @(
  "SimpleBreathing.csproj",
  "SimpleBreathing.sln",
  "assets/icons/floppy-disk.svg",
  "assets/icons/floppy-disk.svg.import"
)

foreach ($path in $paths) {
  if (Test-Path $path) {
    git rm $path
  }
}

if (Test-Path "scripts") {
  Get-ChildItem "scripts" -Filter "*.cs" | ForEach-Object {
    git rm ("scripts/" + $_.Name)
  }
  Get-ChildItem "scripts" -Filter "*.cs.uid" | ForEach-Object {
    git rm ("scripts/" + $_.Name)
  }
}

Write-Host "C# active files neutralized. The .txt reference copies should remain in docs/csharp_reference/."
Write-Host "Now close Godot completely, delete the local .godot folder if the C# error persists, then reopen the project."
