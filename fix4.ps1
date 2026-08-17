$c = [System.IO.File]::ReadAllText('f:\rhyno-automation-1\rhyno-automation\Modules\MowadiyanIntegration\TaxExporterService.cs')

# Find the exact boundaries
$navIdx = $c.IndexOf('page = await NavigateToOutputFilesInNewTabAsync(context, cancellationToken);')
$endOfNavLine = $c.IndexOf([char]10, $navIdx) + 1
$searchStart = $c.LastIndexOf('if (!IsUrlOnPortal(GetPageUrl(page)))', $navIdx)

$oldBlock = $c.Substring($searchStart, $endOfNavLine - $searchStart)

# Build new block — use single quotes to avoid interpolation issues with Persian text
$newBlock = '            if (!IsUrlOnPortal(GetPageUrl(page)))
            {
                _logger.LogInformation("' + [char]0x062F + [char]0x0631 + ' ' + [char]0x062D + [char]0x0627 + [char]0x0644 + ' ' + [char]0x06A9 + [char]0x0644 + [char]0x06CC + [char]0x06A9 + ' ' + [char]0x0631 + [char]0x0648 + [char]0x06CC + ' ' + [char]0x06A9 + [char]0x0627 + [char]0x0631 + [char]0x067E + [char]0x0648 + [char]0x0634 + [char]0x0647 + ' ' + [char]0x062A + [char]0x062C + [char]0x0627 + [char]0x0631 + [char]0x06CC + ' ' + [char]0x0627 + [char]0x0646 + [char]0x062A + [char]0x0642 + [char]0x0627 + [char]0x0644 + ' ' + [char]0x0628 + [char]0x0647 + ' tp.tax.gov.ir...");
                await TryOpenBusinessDashboardAsync(page, cancellationToken);
            }

            _logger.LogInformation("' + [char]0x062F + [char]0x0631 + ' ' + [char]0x062D + [char]0x0627 + [char]0x0644 + ' ' + [char]0x0647 + [char]0x062F + [char]0x0627 + [char]0x06CC + [char]0x062A + ' ' + [char]0x0628 + [char]0x0647 + ' ' + [char]0x0635 + [char]0x0641 + [char]0x062D + [char]0x0647 + ' ' + [char]0x062E + [char]0x0631 + [char]0x0648 + [char]0x062C + [char]0x06CC + ' ' + [char]0x0635 + [char]0x0648 + [char]0x0631 + [char]0x062A + [char]0x062D + [char]0x0633 + [char]0x0627 + [char]0x0628 + [char]0x200C + [char]0x0647 + [char]0x0627 + ' ({Url})...", OutputFilesUrl);

            // Step 6 - Open outputFiles DIRECTLY in a NEW tab with the exact URL and NO extra
            // parameters. The operator explicitly requires:
            //   (1) the browser stays open after a successful login,
            //   (2) no blank tab is left behind,
            //   (3) after login the export page opens in ANOTHER tab,
            //   (4) it goes straight to https://tp.tax.gov.ir/invoice/outputFiles with no
            //       appended query parameters - no intermediate homepage, no /counter, nothing.
            page = await NavigateToOutputFilesInNewTabAsync(context, cancellationToken);
'

$c = $c.Substring(0, $searchStart) + $newBlock + $c.Substring($endOfNavLine)

[System.IO.File]::WriteAllText('f:\rhyno-automation-1\rhyno-automation\Modules\MowadiyanIntegration\TaxExporterService.cs', $c, [System.Text.UTF8Encoding]::new($true))

Write-Output "Done - removed EnsurePortalSessionEstablishedAsync call"
