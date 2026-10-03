#!/bin/bash
# Protobuf build script for Go and C# (Unity)

set -e

PROTO_DIR="$(dirname "$0")"
OUT_GO="../server/internal/proto"
OUT_CS="../client/Assets/Scripts/Generated/Proto"

mkdir -p "$OUT_GO"
mkdir -p "$OUT_CS"

echo "Building Go protos..."
protoc \
  --proto_path="$PROTO_DIR" \
  --go_out="$OUT_GO" \
  --go_opt=paths=source_relative \
  --go-grpc_out="$OUT_GO" \
  --go-grpc_opt=paths=source_relative \
  "$PROTO_DIR"/*.proto

echo "Building C# protos for Unity..."
protoc \
  --proto_path="$PROTO_DIR" \
  --csharp_out="$OUT_CS" \
  --csharp_opt=base_namespace=Megame \
  "$PROTO_DIR"/*.proto

echo "Done!"