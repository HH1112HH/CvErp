# Xuất 6 view báo cáo ra CSV để import vào Power BI (phương án dự phòng khi không nối trực tiếp được SQL Server).
# Chạy: powershell -ExecutionPolicy Bypass -File database\export-views.ps1

$ErrorActionPreference = 'Stop'

# Dùng en-US để CSV có dấu chấm thập phân, dễ import vào Power BI.
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('en-US')

$server   = '.\SQLEXPRESS'
$database = 'CvErp'
$outDir   = Join-Path $PSScriptRoot 'exports'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$views = @(
    'vw_InventoryOnHand',
    'vw_ProductionPerformance',
    'vw_MaterialCostVariance',
    'vw_ProductionOrderProgress',
    'vw_StockMovement',
    'vw_OutputMonthly'
)

$connStr = "Server=$server;Database=$database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

try {
    foreach ($view in $views) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT * FROM dbo.$view"
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $table = New-Object System.Data.DataTable
        [void]$adapter.Fill($table)

        $path = Join-Path $outDir "$view.csv"
        $table | Export-Csv -Path $path -NoTypeInformation -Encoding UTF8
        Write-Host ("{0} -> {1} dòng" -f $view, $table.Rows.Count)
    }
}
finally {
    $conn.Close()
}

Write-Host "Xong. File CSV tại: $outDir"
