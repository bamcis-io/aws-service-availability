$data = Get-Content .\services.json | ConvertFrom-Json # services.json pulled from status.aws.amazon.com javascript

$items = @()

foreach ($item in $data)
{
  $index = $item.IndexOf("-")

  if ($index -le 0)
  {
       $items += $item
  }
  else
  {
     $service = $item.substring(0, $index)

     if ($service -eq "aws")
     {
         $index2 = $item.IndexOf("-", $index + 1)
         $service = $item.substring(0, $index2)
     }

     $items += $service
  }

}

$items | Get-Unique | Sort-Object | Set-Content D:\users\mhaken\source\aws-service-availability\service-availability-function.Tests\uniqueservices.txt