@echo off
echo Registering URL ACL for http://+:5005/ to Users...
netsh http add urlacl url=http://+:5005/ user=Users
pause
