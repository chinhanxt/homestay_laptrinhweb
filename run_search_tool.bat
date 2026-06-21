@echo off
title Homestay Search Tool Launcher
echo Dang khoi dong cong cu tra cuu chuc nang Homestay...
python "%~dp0search_functions.py"
if %errorlevel% neq 0 (
    echo.
    echo [LOI] Khong the chay script Python. 
    echo Vui long kiem tra xem Python da duoc cai dat va duoc them vao bien moi truong PATH chua.
    echo.
    pause
)
