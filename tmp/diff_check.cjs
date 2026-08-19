const { execSync } = require('child_process');
const diff = execSync('git --no-pager diff -- Views/Contracts/Create.cshtml', { encoding: 'utf8' });
const re = /^[+-].*(?:contract-create-layout|form-col|asp-action="[A-Z]|PaymentStatus|PaidAmount|@Model\.Id|form\[asp-action)/;
for (const line of diff.split('\n')) {
  if (re.test(line)) console.log(line);
}
console.log('--- end ---');