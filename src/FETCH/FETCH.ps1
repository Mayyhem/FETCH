<#
.SYNOPSIS
FETCH: Flexible Enumeration Tool for Centrally-managed Hosts.

.DESCRIPTION
Author: SpecterOps
Purpose:
  - Collect local sessions, user rights assignments, and group members
  - Stage output for SharpHound collection via centralized management tools
Requirements:
  - Local Administrators group privileges
  - Domain-joined machine with line of sight to domain controller
  - PowerShell 2.0 or higher
  - .NET Framework 3.5 or higher

.PARAMETER CMPivotDir
Stage results in the specified directory for pickup using CMPivot (default: disabled).

.PARAMETER Debug
Enable debugging of script execution events (default: disabled).

.PARAMETER DeleteOlderThanDays
Delete files and/or class instances older than the specified number of days (default: 7 days).

.PARAMETER LogDir
Store script execution and trace logs in the specified directory. Specify 'off' to disable. (default: $OutputDir if set, otherwise current working directory)

.PARAMETER OutputDir
Store results in the specified directory (default: disabled).

.PARAMETER SessionLookbackDays
Number of days to look back for sessions (default: 7 days).

.PARAMETER Silent
Do not write to the console or any output streams (default: disabled).

.PARAMETER StdOut
Write BloodHound data to the console (default: disabled).

.PARAMETER TempDir
Specifies the path for temporary files created to enumerate user rights (default: $OutputDir if set, otherwise current working directory).

.PARAMETER Trace
Enables trace logging for detailed debugging. This will significantly slow down execution (default: disabled).

.PARAMETER Verbose
Enable verbose logging of script execution events (default: disabled).

.PARAMETER Wmi
Store results in the local WMI repository (default: disabled)

.PARAMETER WmiClassPrefix
Store results in local WMI classes named with the specified prefix (e.g., a value of 'BloodHound_' creates the 'BloodHound_Sessions', 'BloodHound_LocalGroups', and 'BloodHound_UserRights' classes) (default: 'BloodHound_').

.PARAMETER WmiNamespace
Store results in local WMI classes in the specified namespace (default: 'root\cimv2').

.EXAMPLE
.\FETCH.ps1 -Help
# Display help text

.EXAMPLE
.\FETCH.ps1 -Wmi -Verbose
# Output to class instances in the local WMI repository, show verbose messages

.EXAMPLE
.\FETCH.ps1 -SessionLookbackDays 10 -OutputDir C:\Windows\Temp
# Collect sessions from the last 10 days of event logs, output to a local directory

.EXAMPLE
.\FETCH.ps1 -StdOut -Silent
# Output to stdout, silence other output streams

.LINK
https://github.com/BloodHoundAD/SharpHound

#>

[CmdletBinding()]
param(

    [switch]$Help,

    # Validate that the output directory exists
    [ValidateScript({
        if ([string]::IsNullOrEmpty($_) -or (Test-Path $_)) {
            $true
        } else {
            throw "The specified directory does not exist: $_"
        }
    })]
    [string]$CMPivotDir = $null,    

    # Delete data older than the specified number of days when the script is run
    [ValidateRange(1,365)]
    [int]$DeleteOlderThanDays = 7,

    # Validate that the log directory exists
    [ValidateScript({
        if ([string]::IsNullOrEmpty($_) -or $_ -eq "off" -or (Test-Path $_)) {
            $true
        } else {
            throw "The specified directory does not exist: $_"
        }
    })]
    [string]$LogDir = $null,

    # Validate that the output directory exists
    [ValidateScript({
        if ([string]::IsNullOrEmpty($_) -or (Test-Path $_)) {
            $true
        } else {
            throw "The specified directory does not exist: $_"
        }
    })]
    [string]$OutputDir = $null,

    # Number of days behind to fetch sessions
    [ValidateRange(1,365)]
    [int]$SessionLookbackDays = 7,

    [switch]$Silent,

    # Display BloodHound data in console after collection
    [switch]$StdOut,

    # Write temporary files to a specific directory instead of $OutputDir or cwd
    [ValidateScript({
        if ([string]::IsNullOrEmpty($_) -or (Test-Path $_)) {
            $true
        } else {
            throw "The specified directory does not exist: $_"
        }
    })]
    [string]$TempDir = $null,

    # Enable trace logging for debugging (WARNING: This may take a long time)
    [switch]$Trace,

    # Enable storage of results in the local WMI repository
    [switch]$Wmi, 

    # Specify a prefix for naming WMI classes where results are stored 
    [string]$WmiClassPrefix = "BloodHound_",

    # Validate the specified WMI namespace
    [ValidateScript({
        $prefix = $_.Replace('\','/')
        $parts = $prefix -split '/'

        if ($parts.Count -lt 2) {
            throw "The WMI namespace format is 'root\cimv2'."
        }
        $true
    })]
    [string]$WmiNamespace = "root\cimv2"
)


<#
-------------------------------------------
Helper functions
-------------------------------------------
#>

function Add-WmiClass {
    param(
        [string]$WmiNamespace,
        [string]$WmiClassPrefix,
        [string]$CollectionType,
        [hashtable]$KeyProperty,
        [hashtable]$Properties)

    # Example: BloodHound_Sessions
    $wmiClassName = "$WmiClassPrefix$CollectionType"
    $query = "SELECT * FROM meta_class WHERE __class = '$wmiClassName'"
    $result = Get-WmiObject -Namespace $WmiNamespace -Query $query -ErrorAction Stop

    # Create a class to store output if it doesn't exist
    if ($null -eq $result) {

        Write-Log "VERBOSE" "$WmiNamespace\$wmiClassName does not exist, creating it now"

        # Add auto-incrementing key property
        foreach ($key in $KeyProperty.Keys) {
            $propertiesFormatted += "[key] $($KeyProperty[$key]) $key;`n"
        }
        
        # Add other properties
        $Properties.GetEnumerator() | ForEach-Object {
            $propertiesFormatted += "$($_.Value) $($_.Key);"
        }

        $wmiNamespaceFormatted = $WmiNamespace.Replace('\','\\')
        $mofContent = @"
#pragma namespace ("\\\\.\\$wmiNamespaceFormatted")
[ SMS_Report (TRUE),
SMS_Group_Name ("BloodHound $CollectionType"),
SMS_Class_ID ("SpecterOps|BloodHound $CollectionType|1.0")]
class $wmiClassName
{
$($propertiesFormatted.TrimEnd("`n"))
};
"@

        # Save the MOF content to a temporary file
        do {
            $randomPart = [System.IO.Path]::GetRandomFileName().Split('.')[0]
            $tempFilePath = Join-Path -Path $TempDir -ChildPath "temp-$randomPart"
        } while (Test-Path -Path $tempFilePath)

        Set-Content -Path $tempFilePath -Value $mofContent

        # Compile the MOF file
        $mofcomp = $env:SystemRoot + "\system32\wbem\mofcomp.exe"
        $result = & $mofcomp $tempFilePath 2>&1

        # Check if compilation was successful
        if ($LASTEXITCODE -ne 0) {
            Write-Log "ERROR" "Failed to deploy WMI class: $result"
        } else {
            Write-Log "VERBOSE" "Successfully created $WmiNamespace\$wmiClassName"
        }

        # Clean up the temporary file
        Remove-Item -Path $tempFilePath -Force

    } else {
        Write-Log "VERBOSE" "$WmiNamespace\$wmiClassName already exists, skipping creation"
    }
}

function Add-WmiClassInstance {
    param(
        [Parameter(Mandatory=$true)]
        [string]$WmiNamespace,

        [Parameter(Mandatory=$true)]
        [string]$WmiClassPrefix,

        [Parameter(Mandatory=$true)]
        [ValidateSet("Sessions", "LocalGroups", "UserRights")]
        [string]$CollectionType,

        [Parameter(Mandatory=$true)]
        [hashtable]$Properties
    )

    # Construct WMI class name
    $wmiClassName = "$WmiClassPrefix$CollectionType"

    # Create new class instance
    $instance = ([WMICLASS]"\\.\${WmiNamespace}:${wmiClassName}").CreateInstance()

    # Set unique identifier
    $maxID = (Get-WmiObject -Namespace $WmiNamespace -Class $wmiClassName | Measure-Object -Property InstanceID -Maximum).Maximum
    $instance.InstanceID = $maxID + 1

    # Set CollectionDatetime
    $instance.CollectionDatetime = [Management.ManagementDateTimeConverter]::ToDmtfDateTime($(Get-Date))

    # Set properties dynamically
    foreach ($key in $Properties.Keys) {
        if ($instance.PSObject.Properties.Name -contains $key) {
            $instance.$key = if ($Properties[$key] -match '^\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC$') {
                ([DateTime]::ParseExact($Properties[$key], "yyyy-MM-dd HH:mm 'UTC'", [System.Globalization.CultureInfo]::InvariantCulture)).ToString("yyyyMMddHHmmss.ffffff+000")
            } else {
                $Properties[$key]
            }
        } else {
            Write-Log "WARNING" "Property '$key' is not defined in the WMI class '$wmiClassName'. Skipping."
        }
    }

    Write-DebugVar instance

    try {
        $instance.Put() | Out-Null
        Write-Log "VERBOSE" "Successfully saved collection data to ${WmiNamespace}\${wmiClassName}"
    } catch {
        Write-Log "ERROR" "Failed to save class instance. Error: $_"
    }
}

function Remove-OldInstances {
    param(
        [int]$DeleteOlderThanDays,
        [string]$WmiNamespace,
        [string]$WmiClassName
    )

    $instances = Get-WmiObject -Namespace $WmiNamespace -Class $WmiClassName -ErrorAction Stop
    $cutoffDate = (Get-Date).AddDays(-$DeleteOlderThanDays)

    $instancesToDelete = $instances | Where-Object { $_.CollectionDatetime -lt $cutoffDate }
    
    if ($instancesToDelete.Count -gt 0) {
        Write-Log "VERBOSE" "Found $($instancesToDelete.Count) instances older than $DeleteOlderThanDays days. Deleting them."
        foreach ($instance in $instancesToDelete) {
            try {
                $instance.Delete()
                Write-Log "VERBOSE" "Deleted instance with CollectionDatetime: $($instance.CollectionDatetime)"
            }
            catch {
                Write-Log "WARNING" "Failed to delete instance with CollectionDatetime: $($instance.CollectionDatetime). Error: $_"
            }
        }
        Write-Log "VERBOSE" "Cleanup complete. Remaining instances: $((Get-WmiObject -Namespace $WmiNamespace -Class $wmiClassName).Count)"
    }
    else {
        Write-Log "VERBOSE" "Found no instances older than $DeleteOlderThanDays days in $WmiClassName. No cleanup needed."
    }
}

function Write-DebugVar {
    param([string]$VariableName)
    Write-Log "DEBUG" "Line $($MyInvocation.ScriptLineNumber): `$${VariableName} = $(Get-Variable -Name $VariableName -ValueOnly | Out-String)"
}

function Write-Log {
    param(
        [string]$Level,
        [string]$Message
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
    $logEntry = "$timestamp UTC - $Message"
    
    if (-not $Silent) {
        switch ($Level) {
            "DEBUG" { 
                if ($Script:DebugPreference -eq 'Continue') { 
                    Write-Debug $logEntry 
                    if ($LogDir -ne 'off' -and $logFile) { $logEntry | Out-File -FilePath $logFile -Append }
                } 
            }
            "VERBOSE" { 
                if ($Script:VerbosePreference -eq 'Continue') { 
                    Write-Verbose $logEntry
                    if ($LogDir -ne 'off' -and $logFile) { $logEntry | Out-File -FilePath $logFile -Append }
                }
            }
            "INFO" {
                Write-Host "INFO: $logEntry"
                if ($LogDir -ne 'off' -and $logFile) { $logEntry | Out-File -FilePath $logFile -Append }
            }
            "OUTPUT" {
                # Don't include timestamps, send raw message to stdout
                Write-Output $Message
                if ($LogDir -ne 'off' -and $logFile) { $logEntry | Out-File -FilePath $logFile -Append }
            }
            "WARNING" { 
                Write-Warning $logEntry 
                if ($LogDir -ne 'off' -and $logFile) { $logEntry | Out-File -FilePath $logFile -Append }
            }
        }
    } 

    # Always display errors and exit on error
    if ($Level -eq "ERROR") {
        Write-Error $logEntry 
        if ($LogDir -ne 'off' -and $logFile) { $logEntry | Out-File -FilePath $logFile -Append }
        exit 1
    }
}


<#
-------------------------------------------
Main
-------------------------------------------
#>

# Display help text
if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path
    exit
}

$Script:InformationPreference = "Continue"
Write-Log "INFO" "FETCH execution started"

# Catch and log unexpected execution error messages
try {

    if (-not $OutputDir -and -not $StdOut -and -not $Wmi) {
        Write-Log "ERROR" "No output type selected (-OutputDir, -StdOut, -Wmi)"
    }

    # Confirm this is running on a domain-joined machine
    if (-not (Get-WmiObject Win32_ComputerSystem).PartOfDomain) {
        Write-Log "ERROR" "This system is not joined to Active Directory."
    }

    # Confirm this is running in a high integrity context
    if (-not (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Log "ERROR" "This script must be executed with administrator privileges."
    }

    # Collect local system domain computer account SID via LDAP
    $thisComputerName = $env:COMPUTERNAME
    Write-DebugVar thisComputerName

    $thisComputerDomain = (Get-WmiObject Win32_ComputerSystem).Domain
    Write-DebugVar thisComputerDomain

    $thisComputerDomainAccount = New-Object System.Security.Principal.NTAccount("${thisComputerName}$")
    Write-DebugVar thisComputerDomainAccount

    try {
        $thisComputerDomainSID = $thisComputerDomainAccount.Translate([System.Security.Principal.SecurityIdentifier]).Value
        Write-DebugVar thisComputerDomainSID
    } catch {
        Write-Log "ERROR" "Could not translate the domain account to a SID: $sourceComputerDomainAccount"
    }

    # Collect local system FQDN
    $thisComputerFQDN = [System.Net.Dns]::GetHostEntry([string]"localhost").HostName
    Write-DebugVar thisComputerFQDN

    # Get the local machine SID prefixed to local accounts
    $thisComputerMachineSID = ((Get-WmiObject -Class Win32_UserAccount -Filter "LocalAccount='True'" | Select-Object -First 1).SID -replace "-\d+$")
    Write-DebugVar thisComputerMachineSID

    # Initialize logging
    $now = "$((Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss"))-UTC"

    if ($DebugPreference -eq 'Inquire') {
        $Script:DebugPreference = "Continue"
        $Script:VerbosePreference = "Continue"
    }

    # Log to the specified directory, $OutputDir, current working directory, or off
    if ($LogDir -ne "off") {
        if ([string]::IsNullOrEmpty($LogDir)) {
            if (-not [string]::IsNullOrEmpty($OutputDir)) {
                $LogDir = $OutputDir
            } else {
                $LogDir = (Get-Location).Path
            }
        }
        $Script:logFile = "$LogDir\FetchExecution_$thisComputerFQDN`_$now.log"
        Write-Log "INFO" "Writing logs to $logFile"
    }

    # Write temp files to the specified directory, $OutputDir, or cwd
    if ([string]::IsNullOrEmpty($TempDir)) {
        if (-not [string]::IsNullOrEmpty($OutputDir)) {
            $TempDir = $OutputDir
        } else {
            $TempDir = (Get-Location).Path
        }
        Write-Log "INFO" "Writing temp files to $TempDir\"
    }

    # Trace logging - display lines as they are executed and store transcript
    if ($Trace) {
        $tracePath = "$LogDir\FetchTrace_$thisComputerFQDN`_$now.log"
        Write-Log "INFO" "Writing trace to $tracePath"
        Set-PSDebug -Trace 1
        Start-Transcript -Path $tracePath
    }

    # Stage for CMPivot with static file name
    if ($CMPivotDir) { 
        $outputFile = "$CMPivotDir\FetchResults.json" 
        Write-Log "INFO" "Writing results to $outputFile"
    } 

    # Otherwise append file name with computer name and timestamp
    elseif ($OutputDir) { 
        $outputFile = "$OutputDir\FetchResults_$thisComputerFQDN`_$now.json" 
        Write-Log "INFO" "Writing results to $outputFile"
    }

    # Output to stdout
    if ($StdOut) {
        Write-Log "INFO" "Writing results to stdout" 
    }

    # Output to WMI
    if ($Wmi) {
        Write-Log "INFO" "Writing results to: 
                                        $WmiNamespace\$WmiClassPrefix`Sessions
                                        $WmiNamespace\$WmiClassPrefix`UserRights
                                        $WmiNamespace\$WmiClassPrefix`LocalGroups"
    }


    <#
    -------------------------------------------
    Collect sessions
    -------------------------------------------
    #>

    Write-Log "INFO" "Collecting sessions"
    $collectedSessions = @()

    # If using WMI option, create storage class if it doesn't exist
    if ($Wmi) {
        $keyProp = @{ "InstanceID" = "uint32" }
        $props = @{ 
            "CollectionDatetime" = "datetime"
            "UserSID" = "string"
            "LastSeen" = "datetime"
            "ComputerSID" = "string"
        }
        Add-WmiClass -WmiNamespace $WmiNamespace -WmiClassPrefix $WmiClassPrefix -CollectionType "Sessions" -KeyProperty $keyProp -Properties $props
        $existingSessions = Get-WmiObject -Namespace $WmiNamespace -Class $WmiClassPrefix`Sessions
    }

    # Function to add or update a session in $collectedSessions
    function AddOrUpdateSessions([ref]$collectedSessions, $newSession) {

        $collectedSession = $collectedSessions.Value | Where-Object { $_.UserSID -eq $newSession.UserSID -and $_.ComputerSID -eq $newSession.ComputerSID }
        $existingSession = $existingSessions | Where-Object { $_.UserSID -eq $newSession.UserSID -and $_.ComputerSID -eq $newSession.ComputerSID }

        # Check if a session with the same UserSID and ComputerSID was already collected by the script
        if ($collectedSession) {
            Write-Log "VERBOSE" "Duplicate session found in collected data for $($newSession.UserSID) on $($newSession.ComputerSID)"
            # If a session with the same UserSID and ComputerSID is found, compare LastSeen times and update if the new one is more recent
            if ($newSession.LastSeen -gt $collectedSession.LastSeen) {
                $collectedSession.LastSeen = $newSession.LastSeen
                Write-Log "VERBOSE" "Keeping most recent session last seen at $($newSession.LastSeen)"
                $collectedSessions.Value += $newSession
            } else {
                Write-Log "VERBOSE" "Keeping most recent session last seen at $($collectedSession.LastSeen)"
            }
            Write-DebugVar collectedSession

        } else {
            Write-Log "VERBOSE" "Found session for $($newSession.UserSID) on $($newSession.ComputerSID) at $($newSession.LastSeen)"

            if ($Wmi) {

                # Check if a session with the same UserSID and ComputerSID already exists in WMI
                if ($existingSession) {
                    Write-Log "VERBOSE" "Duplicate session found in WMI for $($newSession.UserSID) on $($newSession.ComputerSID)"
                    
                    # Ensure LastSeen formats match
                    $newSessionWmiTimestamp = ([DateTime]::ParseExact($newSession.LastSeen, "yyyy-MM-dd HH:mm 'UTC'", [System.Globalization.CultureInfo]::InvariantCulture)).ToString("yyyyMMddHHmmss.ffffff+000")

                    # If a session with the same UserSID and ComputerSID is found, compare LastSeen times and update if the new one is more recent
                    if ($newSessionWmiTimestamp -gt $existingSession.LastSeen) {
                        Write-Log "VERBOSE" "Keeping most recent session last seen at $($newSession.LastSeen)"
                        $existingSession.Delete()
                        Add-WmiClassInstance -WmiNamespace $WmiNamespace -WmiClassPrefix $WmiClassPrefix -CollectionType 'Sessions' -Properties $newSession
                    } else {
                        Write-Log "VERBOSE" "Keeping most recent session last seen at $($existingSession.LastSeen)"
                    }
                    Write-DebugVar existingSession

                } else {
                    Add-WmiClassInstance -WmiNamespace $WmiNamespace -WmiClassPrefix $WmiClassPrefix -CollectionType 'Sessions' -Properties $newSession
                }
            } 

            # Add the session to the script output
            $collectedSessions.Value += $newSession
            Write-DebugVar newSession
        }
    }

    # Get logged in accounts from HKEY_USERS hive
    Write-Log "VERBOSE" "Evaluating HKEY_USERS"
    $registryKeys = Get-Item -Path "registry::HKEY_USERS\*"
    Write-DebugVar registryKeys

    # Filter keys to those associated with user accounts
    $filteredKeys = $registryKeys | Where-Object {
        $_.Name -match 'S-1-5-21-' -and -not $_.Name.EndsWith('_Classes')
    }
    Write-DebugVar filteredKeys

    foreach ($filteredKey in $filteredKeys) {

        $hkuSID = ($filteredKey.Name -split "\\")[1]
        Write-DebugVar hkuSID

        # Discard local users
        if ($hkuSID -notlike "$thisComputerMachineSID*") {

            # Create a record for each domain user session
            $newSession = @{
                UserSID = $hkuSID
                ComputerSID = $thisComputerDomainSID
                LastSeen = "{0:yyyy-MM-dd HH:mm} UTC" -f (Get-Date).ToUniversalTime()
            }
            AddOrUpdateSessions ([ref]$collectedSessions) $newSession | Out-Null

        } else {
            Write-Log "DEBUG" "Discarding local user with SID: $hkuSID"
        }
    }
    Write-DebugVar collectedSessions

    # Get historic logon data from root\cimv2\sms\SMS_UserLogonEvents
    Write-Log "VERBOSE" "Evaluating SMS_UserLogonEvents"
    $lookbackPeriodFormatted = [int64]((Get-Date).AddDays(-$SessionLookbackDays) - (Get-Date "1970-01-01 00:00:00")).TotalSeconds

    $query = "SELECT * FROM SMS_UserLogonEvents WHERE LogonTime >= '$lookbackPeriodFormatted'"
    $events = Get-WmiObject -Namespace "root\cimv2\sms" -Query $query
    Write-DebugVar events

    # Check if any events were returned
    if ($events) {
        # Process and display the events
        $events | ForEach-Object {
            $logonTime =(Get-Date "1970-01-01 00:00:00").AddSeconds($_.LogonTime).ToUniversalTime()
            $userSID = $_.UserSID

            # Discard local users
            if ($userSID -notlike "$thisComputerMachineSID*") {

                # Create a record for each domain user session
                $newSession = @{
                    UserSID = $userSID
                    ComputerSID = $thisComputerDomainSID
                    LastSeen = "{0:yyyy-MM-dd HH:mm} UTC" -f $logonTime
                }
                AddOrUpdateSessions ([ref]$collectedSessions) $newSession | Out-Null

            } else {
                Write-Log "DEBUG" "Discarding local user with SID: $hkuSID"
            }
        }
    } else {
        Write-Log "INFO" "No logon events found in the lookback period."
    }

    Write-Log "INFO" "Found sessions for $($collectedSessions.Count) domain principals"

    $sessions = @{
        "Results" = $collectedSessions
        "Collected" = $true
        "FailureReason" = $null
    }


    <#
    -------------------------------------------
    Collect local user rights assignments
    -------------------------------------------
    #>

    Write-Log "INFO" "Collecting user rights assignments"
    $userRights = @()

    # If using WMI option, create storage class if it doesn't exist
    if ($Wmi) {
        $keyProp = @{ "InstanceID" = "uint32" }
        $props = @{ 
            "CollectionDatetime" = "datetime"
            "Privilege" = "string"
            "ObjectIdentifier" = "string"
            "ObjectType" = "string"
        }
        Add-WmiClass -WmiNamespace $WmiNamespace -WmiClassPrefix $WmiClassPrefix -CollectionType "UserRights" -KeyProperty $keyProp -Properties $props
        $existingRights = Get-WmiObject -Namespace $WmiNamespace -Class $WmiClassPrefix`UserRights
    }

    # Export the security configuration to a file, discarding non-terminating errors to prevent stdout pollution
    Write-Log "VERBOSE" "Exporting user rights assignments with secedit"
    secedit /export /areas USER_RIGHTS /cfg "$TempDir\secedit.cfg" > $null 2>&1

    # Read the contents of the exported file
    $seceditContents = Get-Content "$TempDir\secedit.cfg" | Out-String
    Write-DebugVar seceditContents

    # Remove the exported file
    Remove-Item "$TempDir\secedit.cfg"

    # Extract and format user rights assignments from the secedit output
    $userRightsLines = $seceditContents -split "`r`n" | Where-Object { $_ -like "SeRemoteInteractiveLogonRight*" }
    Write-DebugVar userRightsLines

    foreach ($line in $userRightsLines) {
        # Split SIDs from rights assignments
        $lineParts = $line -split "="
        $right = $lineParts[0].Trim()
        # Split SIDs into a list
        $sids = $lineParts[1].Trim() -split ","
        $sids = $sids | ForEach-Object {
            # Remove leading asterisk from each SID
            $sid = $_ -replace "^\*", ""
            # Replace built-in local group SIDs with domain computer SID
            $sid = $sid -replace "S-1-5-32", $thisComputerDomainSID
            @{
                "ObjectIdentifier" = $sid
                "ObjectType" = "LocalGroup"
            }
        }
        $userRight = @{
            "Privilege" = $right
            "Results" = $sids
            "LocalNames" = @()
            "Collected" = $true
            "FailureReason" = $null
        }

        foreach ($sid in $sids) {
            $objectIdentifier = $sid.ObjectIdentifier
            $objectType = $sid.ObjectType

            if ($Wmi) {
                $userRightsAssignment = @{
                    "Privilege" = $right
                    "ObjectIdentifier" = $objectIdentifier
                    "ObjectType" = $objectType
                }

                # Check if a user rights assignment with the same permission and user SID already exists
                $matchingRight = $existingRights | Where-Object { $_.Privilege -eq $userRightsAssignment.Privilege -and $_.ObjectIdentifier -eq $userRightsAssignment.ObjectIdentifier }
                
                if ($matchingRight) {
                    Write-Log "VERBOSE" "Skipping duplicate user rights assignment found for $($userRightsAssignment.ObjectIdentifier): $($userRightsAssignment.Privilege)"
                    
                } else {
                    Write-Log "VERBOSE" "Found new user rights assignment for $($userRightsAssignment.ObjectIdentifier): $($userRightsAssignment.Privilege)"

                    if ($Wmi) {
                        Add-WmiClassInstance -WmiNamespace $WmiNamespace -WmiClassPrefix $WmiClassPrefix -CollectionType 'UserRights' -Properties $userRightsAssignment
                    }
                }
            }
        }

        # Add each user rights assignment to script output
        $userRights += $userRight
        Write-DebugVar userRight
    }

    Write-DebugVar userRights
    Write-Log "INFO" "Found user rights for $($userRights.Count) privileges"

    <#
    -------------------------------------------
    Collect local group memberships
    -------------------------------------------
    #>

    Write-Log "INFO" "Collecting local group memberships"
    $groups = @()

    # If using WMI option, create storage class if it doesn't exist
    if ($Wmi) {
        $keyProp = @{ "InstanceID" = "uint32" }
        $props = @{ 
            "CollectionDatetime" = "datetime"
            "GroupName" = "string"
            "GroupSID" = "string"
            "MemberType" = "string"
            "MemberSID" = "string"
        }
        Add-WmiClass -WmiNamespace $WmiNamespace -WmiClassPrefix $WmiClassPrefix -CollectionType "LocalGroups" -KeyProperty $keyProp -Properties $props
        $existingGroupMembers = Get-WmiObject -Namespace $WmiNamespace -Class $WmiClassPrefix`LocalGroups
    }

    $currentGroup = $null

    # Exclude domain controllers from local group collection
    $isDC = (Get-WmiObject -Class Win32_ComputerSystem).DomainRole -ge 4
    Write-DebugVar isDC

    if (-not $isDC) {
        # Create an ADSI object for the local computer
        $computer = [ADSI]("WinNT://$env:COMPUTERNAME,computer")
        Write-DebugVar computer

        # Iterate through each child object under the computer (these are local groups and users)
        $groupsObject = $computer.psbase.children | Where-Object { $_.SchemaClassName -eq 'group' }
        Write-DebugVar groupsObject

        foreach ($group in $groupsObject) {
            Write-DebugVar group

            # Retrieve the name of the group
            $groupName = $group.GetType().InvokeMember("Name", 'GetProperty', $null, $group, $null)
            Write-DebugVar groupName

            # Use WMI to fetch group SID
            $groupSID = (Get-WmiObject Win32_Group -Filter "Name='$groupName'").SID
            Write-DebugVar groupSID

            # Output the group name and member SID
            $currentGroup = @{
                # Replace built-in local group SIDs with domain computer SID
                "ObjectIdentifier" = $($groupSID.Replace("S-1-5-32", $thisComputerDomainSID))
                "Name" = $groupName.ToUpper() + "@" + $thisComputerFQDN.ToUpper()
                "Results" = @()
                "LocalNames" = @()
                "Collected" = $true
                "FailureReason" = $null
            }
            Write-DebugVar currentGroup

            # Iterate through each member of the current group
            $members = $group.psbase.Invoke("Members")
            Write-DebugVar members

            foreach ($member in $members) {
                Write-DebugVar member

                # Start with null output
                $result = $null

                # Retrieve the class of the member to ensure it's a User
                $memberClass = $member.GetType().InvokeMember("Class", 'GetProperty', $null, $member, $null)
                Write-DebugVar memberClass

                # Retrieve name and SID and convert the SID to human-readable format
                $memberName = $member.GetType().InvokeMember("Name", 'GetProperty', $null, $member, $null)
                Write-DebugVar memberName
                $memberSIDBytes = $member.GetType().InvokeMember("objectSid", 'GetProperty', $null, $member, $null)
                Write-DebugVar memberSIDBytes
                $memberSID = (New-Object System.Security.Principal.SecurityIdentifier $memberSIDBytes, 0).Value
                Write-DebugVar memberSID

                switch ($memberClass) {

                    "Group" {

                        # Default groups with well-known SIDs
                        if ($memberSID.Length -lt 14) {

                            # Replace built-in local group SIDs with domain computer SID
                            if ($memberSID -like "S-1-5-32-*") {
                                $memberType = "LocalGroup"
                                $result = @{
                                    "ObjectIdentifier" = $($memberSID.Replace("S-1-5-32", $thisComputerDomainSID))
                                    "ObjectType" = $memberType
                                }

                            # Everyone and Authenticated Users include domain users, so collect them
                            } elseif ($memberSID -eq "S-1-1-0" -or $memberSID -eq "S-1-5-11") {
                                $memberType = "Group"
                                $result = @{
                                    "ObjectIdentifier" = "$($thisComputerDomain.ToUpper())-$memberSID"
                                    "ObjectType" = $memberType
                                }

                            # Skip other built-in SIDs (e.g., IUSR, INTERACTIVE)
                            } else {
                                Write-Log "VERBOSE" "Not collecting members of $memberName ($memberSID)"
                            }

                        # This computer
                        } elseif ($memberSID -eq "$thisComputerMachineSID") {
                            $memberType = "Computer"
                            $result = @{
                                "ObjectIdentifier" = $thisComputerDomainSID
                                "ObjectType" = $memberType
                            }

                        # Non-default local groups
                        } elseif ($memberSID -like "$thisComputerMachineSID*") {
                            $memberType = "LocalGroup"
                            $localGroupID = ($memberSID -split "-")[-1]
                            $result = @{
                                "ObjectIdentifier" = ($thisComputerDomainSID -join $localGroupID)
                                "ObjectType" = $memberType
                            }

                        # Domain groups
                        } else {
                            $memberType = "Group"
                            $result = @{
                                "ObjectIdentifier" = $memberSID
                                "ObjectType" = $memberType
                            }
                        }
                    }

                    "User" {

                        # Skip local users
                        if ($memberSID -notlike "$thisComputerMachineSID*") {

                            # Collect domain users and computers
                            $memberType = "Base"
                            $result = @{
                                "ObjectIdentifier" = $memberSID
                                "ObjectType" = $memberType
                            }
                        } else {
                            $memberType = "LocalUser"
                        }
                    }
                }
                Write-DebugVar result

                if ($result) {
                
                    $currentGroup["Results"] += $result
                    Write-DebugVar currentGroup

                    if ($Wmi) {
                        $localGroupMember = @{
                            "GroupName" = $currentGroup.Name
                            "GroupSID" = $currentGroup.ObjectIdentifier
                            "MemberType" = $memberType
                            "MemberSID" = $memberSID
                        }

                        # Check if a group member with the same user SID already exists
                        $matchingGroupMember = $existingGroupMembers | Where-Object { $_.GroupSID -eq $localGroupMember.GroupSID -and $_.MemberSID -eq $localGroupMember.MemberSID }
                        
                        if ($matchingGroupMember) {
                            Write-Log "VERBOSE" "Skipping duplicate group member: $memberSID in $($currentGroup.Name)"
                            
                        } else {
                            Write-Log "VERBOSE" "Found new $memberType $memberSID in $($currentGroup.Name) ($($currentGroup.ObjectIdentifier))"

                            if ($Wmi) {
                                Add-WmiClassInstance -WmiNamespace $WmiNamespace -WmiClassPrefix $WmiClassPrefix -CollectionType 'LocalGroups' -Properties $localGroupMember
                            }
                        }
                    }
                } else {
                    Write-Log "VERBOSE" "Skipping $memberType $memberSID in $($currentGroup.Name) ($($currentGroup.ObjectIdentifier))"
                }
            }

            # Add each local group to script output
            $groups += $currentGroup
        }
    } else {
        Write-Log "VERBOSE" "This system is a domain controller, skipping local group membership enumeration"
    }

    Write-DebugVar groups
    Write-Log "INFO" "Found membership for $($groups.Count) local groups"


    <#
    -------------------------------------------
    Format output and stage for SharpHound collection
    -------------------------------------------
    #>

    $data = @(
        @{
            ObjectIdentifier = $thisComputerDomainSID
            Properties = @{
                name = $thisComputerFQDN.ToUpper()
            }
            Sessions = $sessions | Sort-Object -Unique
            UserRights = $userRights
            LocalGroups = $groups
        }
    )
    Write-DebugVar data

    $output = @{
        data = $data
        meta = @{
            methods = 107028
            type = "computers"
            count = $data.Count
            # Version is also replaced by SharpHound before upload to ingest API
            version = 5
        }
    }
    Write-DebugVar output

    # JSON converter for PowerShell 2.0 compatibility
    function ConvertTo-CustomJson {
        param (
            [hashtable] $hash
        )
        $output = ""

        function Convert-Item ($item) {
            if ($item -is [string]) {
                return '"' + $item + '"'
            } elseif ($item -is [int]) {
                return $item
            } elseif ($item -is [bool]) {
                return $item.ToString().ToLower()
            } elseif ($item -is [array]) {
                $arr = @($item | ForEach-Object { Convert-Item $_ })
                return '[' + ($arr -join ",") + ']'
            } elseif ($item -is [hashtable]) {
                $obj = @()
                $item.Keys | ForEach-Object {
                    $key = $_
                    $value = $item[$key]
                    $obj += ('"' + $key + '":' + (Convert-Item $value))
                }
                return '{' + ($obj -join ",") + '}'
            } else {
                return 'null'
            }
        }

        $hash.Keys | ForEach-Object {
            $key = $_
            $value = $hash[$key]
            $output += ('"' + $key + '":' + (Convert-Item $value) + ',')
        }

        # Remove trailing comma and wrap with curly braces
        return '{' + $output.TrimEnd(",") + '}'
    }
    $jsonOutput = ConvertTo-CustomJson $output
    Write-DebugVar jsonOutput

    # Write to file if output directory is specified
    if ($OutputDir) {
        # BOM
        #$jsonOutput | Out-File $outputFile -Encoding utf8
        # No BOM
        [System.IO.File]::WriteAllBytes($outputFile, [System.Text.Encoding]::UTF8.GetBytes($jsonOutput))
        Write-Log "INFO" "Results saved to $outputFile"
    }

    # Write to stdout if specified
    if ($StdOut) {
        Write-Log "OUTPUT" $jsonOutput
    }

    if ($Wmi -and $null -ne $DeleteOlderThanDays) {
        Remove-OldInstances -DeleteOlderThanDays $DeleteOlderThanDays -WmiNamespace $WmiNamespace -WmiClassName "$WmiClassPrefix`Sessions"
        Remove-OldInstances -DeleteOlderThanDays $DeleteOlderThanDays -WmiNamespace $WmiNamespace -WmiClassName "$WmiClassPrefix`UserRights"
        Remove-OldInstances -DeleteOlderThanDays $DeleteOlderThanDays -WmiNamespace $WmiNamespace -WmiClassName "$WmiClassPrefix`LocalGroups"
    }

} catch {
    Write-Log "ERROR" "FETCH encountered an error at line $($_.InvocationInfo.ScriptLineNumber): $($_.Exception.Message)"

} finally {
    # End logging
    Write-Log "INFO" "FETCH execution completed"

    # Disable trace logging
    if ($Trace) {
        Set-PSDebug -Off
        Stop-Transcript
    }
} 