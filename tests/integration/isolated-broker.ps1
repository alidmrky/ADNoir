# Controls only the dedicated adnoir_closure node, never the Windows RabbitMQ service.
param([switch]$Stop)
$ErrorActionPreference='Stop'
$base=(Resolve-Path "$PSScriptRoot/../..").Path
$runPath=Join-Path $base 'artifacts/checks/broker-closure'
if(-not $Stop){New-Item -ItemType Directory -Force -Path $runPath | Out-Null}
$run=(Resolve-Path $runPath).Path
$sbin='C:/Program Files/RabbitMQ Server/rabbitmq_server-4.3.6/sbin'
$settings=@{RABBITMQ_BASE=$run;RABBITMQ_MNESIA_BASE="$run/db";RABBITMQ_LOG_BASE="$run/log";RABBITMQ_NODENAME="adnoir_closure@$env:COMPUTERNAME";RABBITMQ_NODE_PORT='16672';RABBITMQ_DIST_PORT='26672';RABBITMQ_CONFIG_FILE="$run/rabbitmq.conf";RABBITMQ_ENABLED_PLUGINS_FILE="$run/enabled_plugins";RABBITMQ_CONF_ENV_FILE="$run/rabbitmq-env-conf.bat"}
$script=if($Stop){'rabbitmqctl.bat'}else{'rabbitmq-server.bat'}
if(!$Stop){
 if(Get-NetTCPConnection -LocalPort 16672,26672 -State Listen -ErrorAction SilentlyContinue){throw 'Fixture broker ports in use'}
 "listeners.tcp.default = 16672`ndistribution.listener.port_range.min = 26672`ndistribution.listener.port_range.max = 26672" | Set-Content "$run/rabbitmq.conf"
 '[].' | Set-Content "$run/enabled_plugins"
 '@echo off' | Set-Content "$run/rabbitmq-env-conf.bat"
}
$argsText='/c ""'+$sbin+'/'+$script+'"'+$(if($Stop){' shutdown'}else{''})+'"'
$p=Start-Process cmd.exe -ArgumentList $argsText -Environment $settings -WorkingDirectory $run -WindowStyle Hidden -PassThru -RedirectStandardOutput "$run/$script.out.log" -RedirectStandardError "$run/$script.err.log"
if($Stop){$p.WaitForExit();if($p.ExitCode -ne 0){throw 'Fixture broker shutdown failed'}}else{$p.Id | Set-Content "$run/launcher.pid";Write-Host "Fixture broker launcher PID $($p.Id)"}
