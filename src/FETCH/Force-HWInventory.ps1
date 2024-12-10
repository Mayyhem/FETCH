Get-Service -Name CcmExec | Stop-Service -Force
Sleep -Seconds 5
Remove-Item -Path C:\Windows\CCM\ServiceData\Messaging\EndpointQueues\InventoryAgent -Recurse -Force -Confirm:$false
Get-Service -Name CcmExec | Start-Service
Sleep -Seconds 10
Get-WmiObject -NameSpace ROOT\ccm\InvAgt -Query "SELECT * FROM InventoryActionStatus WHERE InventoryActionID='{00000000-0000-0000-0000-000000000001}'" | Remove-WmiObject
Invoke-WmiMethod -Namespace ROOT\ccm -Class SMS_Client -Name TriggerSchedule "{00000000-0000-0000-0000-000000000001}"