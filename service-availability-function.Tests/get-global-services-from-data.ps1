$data = Get-Content D:\users\mhaken\source\aws-service-availability\service-availability-function.Tests\services.json | ConvertFrom-Json
$regex = "((?:us|eu|cn|ap|ca|me|sa|af)(?:-gov|-isob?)?-(?:(?:(?:central|(?:north|south)?(?:east|west)?)-\d)|standard))"

$items = @()

foreach ($item in $data)
{
   if ($item -notmatch $regex)
   {
    $items += $item
   }
}

$items | Get-Unique | Sort-Object | Set-Content D:\users\mhaken\source\aws-service-availability\service-availability-function.Tests\globalservices.txt