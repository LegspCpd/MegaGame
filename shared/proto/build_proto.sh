#!/bin/bash
# Generate protobuf sources for Go and C# (Unity).
#
# Paths are resolved relative to this script, not the caller's working
# directory, so it behaves the same whether it is run from the repo root or
# from shared/proto.

set -euo pipefail

PROTO_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${PROTO_DIR}/../.." && pwd)"

GO_MODULE="github.com/megame/server"
OUT_GO="${REPO_ROOT}/server"
OUT_CS="${REPO_ROOT}/client/Assets/Scripts/Generated/Proto"

command -v protoc >/dev/null || { echo "protoc not found" >&2; exit 1; }

echo "Building Go protos..."
mkdir -p "${OUT_GO}"
# Each proto declares its own go_package (…/internal/proto/<pkg>), so the
# module prefix is stripped and output lands in internal/proto/<pkg>/.
protoc \
  --proto_path="${PROTO_DIR}" \
  --go_out="${OUT_GO}" \
  --go_opt="module=${GO_MODULE}" \
  --go-grpc_out="${OUT_GO}" \
  --go-grpc_opt="module=${GO_MODULE}" \
  "${PROTO_DIR}"/*.proto

echo "Building C# protos for Unity..."
rm -rf "${OUT_CS}"
mkdir -p "${OUT_CS}"
protoc \
  --proto_path="${PROTO_DIR}" \
  --csharp_out="${OUT_CS}" \
  --csharp_opt=base_namespace=Megame \
  "${PROTO_DIR}"/*.proto
# protoc emits one directory per proto package; Unity expects them flat.
find "${OUT_CS}" -mindepth 2 -name '*.cs' -exec mv -t "${OUT_CS}" {} +
find "${OUT_CS}" -mindepth 1 -type d -empty -delete

echo "Done."