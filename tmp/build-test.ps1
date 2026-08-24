cd "f:\rhyno-automation-1\rhyno-automation"
dotnet build --no-restore -v m 2>&1 | Select-String -Pattern "error|Build succeeded|Build FAILED|Error\(s\)|Warning\(s\)"