C:\Factory\Tools\RDMD.exe /RC out

COPY /B Client\Claes20200001\bin\Release\Claes20200001.exe out\Client.exe
COPY /B Server\Claes20200001\bin\Release\Claes20200001.exe out\Server.exe
C:\Factory\Tools\xcp.exe doc out

C:\Factory\SubTools\zip.exe /O out HTTCmd
