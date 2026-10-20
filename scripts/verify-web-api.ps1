param([string]$BaseUrl = 'http://127.0.0.1:5036')
$ErrorActionPreference = 'Stop'
function Assert($Condition, $Description) {
    if (-not $Condition) { throw "Falhou: $Description" }
    Write-Output "OK: $Description"
}
function CallApi($Path, $Method = 'GET', $Body = $null, $Headers = @{}) {
    $parameters = @{ Uri = "$BaseUrl/api$Path"; Method = $Method; Headers = $Headers }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json; charset=utf-8'
        $parameters.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 12))
    }
    $result = Invoke-RestMethod @parameters
    if ($result -is [array]) { foreach ($item in $result) { Write-Output $item } }
    elseif ($null -ne $result) { Write-Output $result }
}
function ExpectStatus($Expected, $Action, $Description) {
    $status = 0
    try { & $Action | Out-Null } catch { if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode } else { throw } }
    Assert ($status -eq $Expected) $Description
}
$suffix = [Guid]::NewGuid().ToString('N')
$credentials = @{ nome = 'Teste web'; email = "web-$suffix@example.test"; senha = [Guid]::NewGuid().ToString('N') }
$user = CallApi '/auth/register' 'POST' $credentials
$headers = @{ Authorization = "Bearer $($user.token)" }
$login = CallApi '/auth/login' 'POST' $credentials
Assert ($login.id -eq $user.id) 'Cadastro e login'
ExpectStatus 401 { CallApi '/triagens' } 'Acesso anonimo bloqueado'
$list = @(CallApi '/triagens' 'GET' $null $headers)
Assert (($list | Where-Object padrao).Count -eq 7) 'Sete triagens do catalogo MAUI'
$detail = CallApi "/triagens/$($list[0].id)" 'GET' $null $headers
$response = @{ nomePaciente = 'Paciente de teste'; idade = 30; sexo = 'Outro'; respostas = @($detail.perguntas | ForEach-Object { @{ perguntaId = $_.id; valor = $true; opcoesSelecionadas = @($_.opcoes) } }) }
$result = CallApi "/triagens/$($detail.id)/responder" 'POST' $response $headers
$total = ($detail.perguntas | ForEach-Object { $_.peso * [Math]::Max(1, @($_.opcoes).Count) } | Measure-Object -Sum).Sum
Assert ($result.pontuacao -eq $total -and $result.pontuacaoMaxima -eq $total) 'Pontuacao calculada pelos pesos no servidor'
$history = @(CallApi "/triagens/$($detail.id)/historico" 'GET' $null $headers)
Assert ($history.Count -eq 1 -and $history[0].nome -eq 'Paciente de teste') 'Historico persistido'
$response.idade = 131
ExpectStatus 400 { CallApi "/triagens/$($detail.id)/responder" 'POST' $response $headers } 'Idade fora do limite rejeitada'
$response.idade = 30
$response.respostas = @(@{ perguntaId = $detail.perguntas[0].id; valor = $true })
ExpectStatus 400 { CallApi "/triagens/$($detail.id)/responder" 'POST' $response $headers } 'Respostas incompletas rejeitadas'
$response.respostas = @($detail.perguntas | ForEach-Object { @{ perguntaId = $detail.perguntas[0].id; valor = $true } })
ExpectStatus 400 { CallApi "/triagens/$($detail.id)/responder" 'POST' $response $headers } 'Respostas duplicadas rejeitadas'
$model = @{ titulo = 'Modelo de teste'; publicoAlvo = 'Adultos'; descricao = 'Teste'; icone = 'T'; perguntas = @(@{ texto = 'Sinal de alerta?'; peso = 2 }); faixas = @(@{ titulo = 'Baixo'; pontuacaoMin = 0; pontuacaoMax = 0; recomendacao = 'Acompanhar'; cor = '#10B981' }, @{ titulo = 'Alto'; pontuacaoMin = 1; pontuacaoMax = 2; recomendacao = 'Avaliar'; cor = '#EF4444' }) }
$created = CallApi '/triagens' 'POST' $model $headers
Assert ($created.criadorUsuarioId -eq $user.id) 'Criacao de triagem particular'
$model.titulo = 'Modelo editado'
CallApi "/triagens/$($created.id)" 'PUT' $model $headers | Out-Null
$updated = CallApi "/triagens/$($created.id)" 'GET' $null $headers
Assert ($updated.titulo -eq 'Modelo editado') 'Edicao de triagem'
CallApi "/usuarios/$($user.id)/home" 'PUT' @{ itens = @(@{ triagemModeloId = $created.id; visivel = $false; ordem = 0 }) } $headers | Out-Null
$homeItems = @(CallApi '/triagens' 'GET' $null $headers)
Assert (-not ($homeItems | Where-Object id -eq $created.id).visivelNaHome) 'Configuracao de home persistida e cache invalidado'
$other = CallApi '/auth/register' 'POST' @{ nome = 'Outro'; email = "other-$suffix@example.test"; senha = $credentials.senha }
$otherHeaders = @{ Authorization = "Bearer $($other.token)" }
ExpectStatus 404 { CallApi "/triagens/$($created.id)" 'GET' $null $otherHeaders } 'Triagem particular isolada por usuario'
ExpectStatus 400 { CallApi "/triagens/$($created.id)" 'PUT' $model $otherHeaders } 'Edicao por outro usuario bloqueada'
ExpectStatus 400 { CallApi "/triagens/$($created.id)/responder" 'POST' @{ nomePaciente = 'Teste'; idade = 30; sexo = 'Outro'; respostas = @(@{ perguntaId = $updated.perguntas[0].id; valor = $true }) } $otherHeaders } 'Execucao de triagem particular por outro usuario bloqueada'
$otherHistory = @(CallApi "/triagens/$($detail.id)/historico" 'GET' $null $otherHeaders)
Assert ($otherHistory.Count -eq 0) 'Historico isolado por usuario'
$excel = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/triagens/$($detail.id)/historico/excel" -Headers $headers
Assert ($excel.Headers['Content-Type'] -like '*spreadsheetml*' -and $excel.RawContentLength -gt 1000) 'Exportacao XLSX autenticada'
CallApi "/triagens/$($created.id)" 'DELETE' $null $headers | Out-Null
ExpectStatus 404 { CallApi "/triagens/$($created.id)" 'GET' $null $headers } 'Exclusao desativa a triagem'
Write-Output 'Verificacao concluida. Use somente banco de teste: este script cria usuarios e historico.'

