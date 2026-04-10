param(
    [datetime]$TimestampUtc = [DateTime]::UtcNow
)

$major = (($TimestampUtc.Year - 2020) * 12) + $TimestampUtc.Month
$minor = $TimestampUtc.Day
$build = [int][Math]::Floor(($TimestampUtc.TimeOfDay.TotalSeconds * 65535) / 86399)

'{0}.{1}.{2}' -f $major, $minor, $build
