$plugin_root = Get-Location
$obs_root = "$plugin_root/../.."
$build_dir = "$obs_root/build"
Set-Location $obs_root
cmake -G "Visual Studio 18 2026" -A x64 -B $build_dir
Set-Location $build_dir
cmake --build . --config Release --target HotCueMesh
Set-Location $plugin_root
