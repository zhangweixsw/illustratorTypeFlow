[CmdletBinding()]
param(
    [ValidateSet('CanvasTextEditing', 'NotEditing', 'Unavailable')]
    [string]$State = 'CanvasTextEditing'
)

$pipe = [IO.Pipes.NamedPipeClientStream]::new(
    '.',
    'IllustratorTypeFlow.v1',
    [IO.Pipes.PipeDirection]::Out)
try {
    $pipe.Connect(2000)
    $writer = [IO.StreamWriter]::new($pipe)
    $writer.AutoFlush = $true
    $payload = @{
        protocol = 1
        state = $State
        pid = $PID
        timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    } | ConvertTo-Json -Compress
    $writer.WriteLine($payload)
    Start-Sleep -Milliseconds 750
    $writer.Dispose()
} finally {
    $pipe.Dispose()
}
