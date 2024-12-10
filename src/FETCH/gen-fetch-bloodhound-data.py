import json
import random
import string

def generate_sid():
    return str(random.randint(1000, 99999))

def generate_name():
    return ''.join(random.choices(string.ascii_uppercase + string.digits, k=8))

def generate_users(count):
    users = []
    for _ in range(count):
        sid = generate_sid()
        name = generate_name()
        user = {
            "Aces": [
                {
                    "InheritanceHash": None,
                    "IsInherited": False,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-512",
                    "PrincipalType": "Group",
                    "RightName": "Owns"
                },
                {
                    "InheritanceHash": "",
                    "IsInherited": False,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-548",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "",
                    "IsInherited": False,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-512",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "787db6920d72eb305ebb412790858149",
                    "IsInherited": True,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-526",
                    "PrincipalType": "Group",
                    "RightName": "AddKeyCredentialLink"
                },
                {
                    "InheritanceHash": "c02930f39297263bbb88b8b4af7288aa",
                    "IsInherited": True,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-527",
                    "PrincipalType": "Group",
                    "RightName": "AddKeyCredentialLink"
                },
                {
                    "InheritanceHash": "a640e79fa2fa02f247b513cc1feabedd",
                    "IsInherited": True,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-519",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "WriteDacl"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "WriteOwner"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "AllExtendedRights"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "GenericWrite"
                }
            ],
            "AllowedToDelegate": [],
            "ContainedBy": {
                "ObjectIdentifier": "B4D21B17-93EF-475E-9910-8E9443462707",
                "ObjectType": "Container"
            },
            "HasSIDHistory": [],
            "IsACLProtected": True,
            "IsDeleted": False,                    
            "ObjectIdentifier": f"S-1-5-21-1642199630-664550351-1777980924-{sid}",
            "PrimaryGroupSID": "S-1-5-21-1642199630-664550351-1777980924-513",                  # Users
            "Properties": {
                "admincount": True,
                "description": None,
                "displayname": f"{name}",               
                "distinguishedname": f"CN={name},CN=USERS,DC=APERTURE,DC=LOCAL",
                "domain": "APERTURE.LOCAL",
                "domainsid": "S-1-5-21-1642199630-664550351-1777980924",
                "dontreqpreauth": False,
                "email": None,
                "enabled": True,
                "encryptedtextpwdallowed": False,
                "hasspn": False,
                "homedirectory": None,
                "isaclprotected": True,
                "lastlogon": 1725988591,
                "lastlogontimestamp": 1725573370,
                "lockedout": False,
                "logonscript": None,
                "logonscriptenabled": False,                
                "name": f"{name}@APERTURE.LOCAL",
                "passwordcantchange": False,
                "passwordexpired": False,
                "passwordnotreqd": False,
                "profilepath": None,
                "pwdlastset": 1715284220,
                "pwdneverexpires": True,
                "samaccountname": f"{name}",
                "sensitive": False,
                "serviceprincipalnames": [],
                "sfupassword": None,
                "sidhistory": [],
                "smartcardrequired": False,
                "supportedencryptiontypes": [
                    "Not defined"
                ],
                "title": None,
                "trustedtoauth": False,
                "unconstraineddelegation": False,
                "unicodepassword": None,
                "unixpassword": None,
                "usedeskeyonly": False,
                "useraccountcontrol": 66048,
                "userpassword": None,
                "whencreated": 1684345625,
            },
            "SPNTargets": []
        }
        users.append(user)
    return users

def generate_computers(count):
    computers = []
    for _ in range(count):
        sid = generate_sid()
        name = generate_name()
        computer = {
            "Aces": [
                {
                    "InheritanceHash": None,
                    "IsInherited": False,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-512",
                    "PrincipalType": "Group",
                    "RightName": "Owns"
                },
                {
                    "InheritanceHash": "",
                    "IsInherited": False,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-548",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "",
                    "IsInherited": False,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-512",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "",
                    "IsInherited": False,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-512",
                    "PrincipalType": "Group",
                    "RightName": "WriteAccountRestrictions"
                },
                {
                    "InheritanceHash": "787db6920d72eb305ebb412790858149",
                    "IsInherited": True,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-526",
                    "PrincipalType": "Group",
                    "RightName": "AddKeyCredentialLink"
                },
                {
                    "InheritanceHash": "c02930f39297263bbb88b8b4af7288aa",
                    "IsInherited": True,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-527",
                    "PrincipalType": "Group",
                    "RightName": "AddKeyCredentialLink"
                },
                {
                    "InheritanceHash": "a640e79fa2fa02f247b513cc1feabedd",
                    "IsInherited": True,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-519",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "WriteDacl"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "WriteOwner"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "GenericWrite"
                }
            ],
            "AllowedToAct": [],
            "AllowedToDelegate": [],
            "ContainedBy": {
                "ObjectIdentifier": "44F21FCB-6B5E-4632-AB23-B19FD5C91C15",
                "ObjectType": "Container"
            },
            "DCRegistryData": {
                "CertificateMappingMethods": None,
                "StrongCertificateBindingEnforcement": None
            },
            "DomainSID": "S-1-5-21-1642199630-664550351-1777980924",
            "DumpSMSAPassword": [],
            "HasSIDHistory": [],
            "IsACLProtected": False,
            "IsDC": False,
            "IsDeleted": False,
            "LocalGroups": [
                {
                    "Collected": True,
                    "FailureReason": None,
                    "LocalNames": [],
                    "Name": f"ADMINISTRATORS@{name}.APERTURE.LOCAL",
                    "ObjectIdentifier": f"S-1-5-21-1642199630-664550351-1777980924-{sid}-544",
                    "Results": []
                },
                {
                    "Collected": True,
                    "FailureReason": None,
                    "LocalNames": [],
                    "Name": f"REMOTE DESKTOP USERS@{name}.APERTURE.LOCAL",
                    "ObjectIdentifier": f"S-1-5-21-1642199630-664550351-1777980924-{sid}-555",
                    "Results": []
                }
            ],
            "ObjectIdentifier": f"S-1-5-21-1642199630-664550351-1777980924-{sid}",
            "PrimaryGroupSID": "S-1-5-21-1642199630-664550351-1777980924-515",                  # Computers
            "PrivilegedSessions": {
                "Collected": False,
                "FailureReason": None,
                "Results": []
            },            
            "Properties": {
                "distinguishedname": f"CN={name},CN=COMPUTERS,DC=APERTURE,DC=LOCAL",
                "domain": "APERTURE.LOCAL",
                "domainsid": "S-1-5-21-1642199630-664550351-1777980924",
                "email": None,
                "enabled": True,
                "encryptedtextpwdallowed": False,
                "haslaps": False,
                "isaclprotected": False,
                "isdc": False,
                "lastlogon": 1715392279,
                "lastlogontimestamp": 1715362436,
                "lockedout": False,
                "logonscriptenabled": False,                
                "name": f"{name}.APERTURE.LOCAL",
                "operatingsystem": "Windows 11 Pro",
                "passwordexpired": False,
                "pwdlastset": 1715362436,                
                "samaccountname": f"{name}$",
                "serviceprincipalnames": [],
                "sidhistory": [],
                "supportedencryptiontypes": [],
                "trustedtoauth": False,
                "unconstraineddelegation": False,
                "usedeskeyonly": False,
                "useraccountcontrol": 4096,
                "whencreated": 1700685901
            },
            "RegistrySessions": {
                "Collected": False,
                "FailureReason": None,
                "Results": []
            },            
            "Sessions": {
                "Collected": True,
                "FailureReason": None,
                "Results": []
            },
            "Status": {
                "Connectable": False,
                "Error": "NotActive"
            },
            "UserRights": [
                {
                    "Collected": True,
                    "FailureReason": None,
                    "LocalNames": [],
                    "Privilege": "SeRemoteInteractiveLogonRight",
                    "Results": [
                        {
                            "ObjectIdentifier": f"S-1-5-21-1642199630-664550351-1777980924-{sid}-544",
                            "ObjectType": "LocalGroup"
                        },
                        {
                            "ObjectIdentifier": f"S-1-5-21-1642199630-664550351-1777980924-{sid}-555",
                            "ObjectType": "LocalGroup"
                        }
                    ]
                }
            ]
        }
        computers.append(computer)
    return computers

def generate_groups(count):
    groups = []
    for _ in range(count):
        sid = generate_sid()
        name = generate_name()
        group = {
            "Aces": [
                {
                    "InheritanceHash": None,
                    "IsInherited": False,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "Owns"
                },
                {
                    "InheritanceHash": "",
                    "IsInherited": False,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-548",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "",
                    "IsInherited": False,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-512",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "a640e79fa2fa02f247b513cc1feabedd",
                    "IsInherited": True,
                    "PrincipalSID": "S-1-5-21-1642199630-664550351-1777980924-519",
                    "PrincipalType": "Group",
                    "RightName": "GenericAll"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "WriteDacl"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "WriteOwner"
                },
                {
                    "InheritanceHash": "46b66b0abd4e73f940c5acadaf59fd61",
                    "IsInherited": True,
                    "PrincipalSID": "APERTURE.LOCAL-S-1-5-32-544",
                    "PrincipalType": "Group",
                    "RightName": "GenericWrite"
                }
            ],
            "ContainedBy": {
                "ObjectIdentifier": "S-1-5-21-1642199630-664550351-1777980924",
                "ObjectType": "Domain"
            },
            "IsACLProtected": False,
            "IsDeleted": False,
            "Members": [],
            "ObjectIdentifier": f"S-1-5-21-1642199630-664550351-1777980924-{sid}",
            "Properties": {
                "admincount": False,
                "description": None,                
                "distinguishedname": f"CN={name},CN=USERS,DC=APERTURE,DC=LOCAL",
                "domain": "APERTURE.LOCAL",
                "domainsid": "S-1-5-21-1642199630-664550351-1777980924",
                "isaclprotected": False,
                "name": f"{name}@APERTURE.LOCAL",
                "samaccountname": name,
                "whencreated": 1684345625
            }
        }
        groups.append(group)
    return groups

def add_group_members(groups, users, computers):
    all_objects = users + computers + groups
    for group in groups:
        member_count = random.randint(1, 5)
        for _ in range(member_count):
            member = random.choice(all_objects)
            object_type = "User" if member in users else "Computer" if member in computers else "Group"
            group["Members"].append({
                "ObjectIdentifier": member["ObjectIdentifier"],
                "ObjectType": object_type
            })

def add_local_group_members(computers, users, groups):
    all_objects = users + computers + groups
    for computer in computers:
        for local_group in computer["LocalGroups"]:
            member_count = random.randint(1, 5)
            for _ in range(member_count):
                member = random.choice(all_objects)
                object_type = "Group" if member in groups else "Base"
                local_group["Results"].append({
                    "ObjectIdentifier": member["ObjectIdentifier"],
                    "ObjectType": object_type
                })

def add_sessions(computers, users):
    all_objects = users + computers
    for computer in computers:
        session_count = random.randint(0, 2)
        for _ in range(session_count):
            user = random.choice(all_objects)
            computer["Sessions"]["Results"].append({
                "ComputerSID": computer["ObjectIdentifier"],
                "LastSeen": "2024-09-18 16:32 UTC",
                "UserSID": user["ObjectIdentifier"]
            })

def generate_test_data(user_count, computer_count, group_count):
    users = generate_users(user_count)
    computers = generate_computers(computer_count)
    groups = generate_groups(group_count)

    add_group_members(groups, users, computers)
    add_local_group_members(computers, users, groups)
    add_sessions(computers, users)

    domain_sid = "S-1-5-21-1642199630-664550351-1777980924"

    users_data = {
        "data": users,
        "meta": {
            "collectorversion": "2.5.6.0",
            "count": len(users),
            "methods": 291819,
            "type": "users",
            "version": 6
        }
    }

    computers_data = {
        "data": computers,
        "meta": {
            "collectorversion": "2.5.6.0",
            "count": len(computers),
            "methods": 291819,
            "type": "computers",
            "version": 6
        }
    }

    groups_data = {
        "data": groups,
        "meta": {
            "collectorversion": "2.5.6.0",
            "count": len(groups),
            "methods": 291819,
            "type": "groups",
            "version": 6
        }
    }

    with open(f"users-{domain_sid}.json", "w") as f:
        json.dump(users_data, f, indent=4)

    with open(f"computers-{domain_sid}.json", "w") as f:
        json.dump(computers_data, f, indent=4)

    with open(f"groups-{domain_sid}.json", "w") as f:
        json.dump(groups_data, f, indent=4)

if __name__ == "__main__":
    user_count = 100000
    computer_count = 50000
    group_count = 500
    generate_test_data(user_count, computer_count, group_count)
