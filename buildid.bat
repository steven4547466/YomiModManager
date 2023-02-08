@echo off
for /f %%a in (export/buildid.txt) do (
  echo %%a
  set /a num=%%a
)
echo %num%
set /a num += 1
echo %num% > export/buildid.txt
echo int buildid = %num%; > buildid.c