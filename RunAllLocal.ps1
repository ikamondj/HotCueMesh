
Start-Process -FilePath ".\PersistenceService\gradlew.bat" `
    -ArgumentList "clean build bootRun" `
    -WorkingDirectory "PersistenceService" `



#Start-Process powershell "go run main.go" -WorkingDirectory "OrchestrationService"

Set-Location "OrchestrationService"
go clean
go build -o OrchestrationService.exe main.go
Set-Location ".."
Start-Process -FilePath ".\OrchestrationService\OrchestrationService.exe"

Set-Location "customizer-interface"
npm install
npm run build
npm start
