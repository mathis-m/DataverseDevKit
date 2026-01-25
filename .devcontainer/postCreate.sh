#!/bin/bash
set -e

echo "Installing pnpm..."
npm install -g pnpm

echo "Installing .NET workloads..."
dotnet workload install maui --ignore-failed-sources || true

echo "Restoring .NET packages..."
cd src/dotnet
dotnet restore || true

echo "Installing web dependencies..."
cd ../web
pnpm install || true

echo "Setup complete!"
