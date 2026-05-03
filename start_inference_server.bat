@echo off
title Roboflow Local Inference Server
echo.
echo  ┌─────────────────────────────────────────────────┐
echo  │  BuildABot — Roboflow Local Inference Server    │
echo  │  Listening on http://192.168.254.113:9001       │
echo  └─────────────────────────────────────────────────┘
echo.
echo  Keep this window open while using the app.
echo  Press Ctrl+C to stop the server.
echo.

:: Start the inference server on all interfaces so the Android device can reach it
inference server start --port 9001 --host 0.0.0.0

pause
