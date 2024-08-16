@echo off
rm -f Release.zip
rem Move to publish folder
cd "MultiUserRaffleBot.Desktop\publish\"
rem do some cleanup
rm -f *.pdb
if exist MultiUserRaffleBot.Desktop.exe (
rm -f MultiUserRaffleBot.exe
rem rename binary
rename MultiUserRaffleBot.Desktop.exe MultiUserRaffleBot.exe
)

rem make zip
7z a ..\..\Release.zip *.dll *.exe ..\..\README.md ..\..\LICENSE
@echo on