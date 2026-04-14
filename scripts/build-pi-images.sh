#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required but not installed." >&2
  exit 1
fi

if ! docker buildx version >/dev/null 2>&1; then
  echo "docker buildx is required but not available." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "docker daemon is not running or not reachable." >&2
  exit 1
fi

REGISTRY="${REGISTRY:-}"
IMAGE_NAMESPACE="${IMAGE_NAMESPACE:-meal-prep}"
IMAGE_TAG="${IMAGE_TAG:-}"
PLATFORMS="${PLATFORMS:-linux/arm64}"
BUILDER_NAME="${BUILDER_NAME:-meal-prep-builder}"
PUSH_LATEST="${PUSH_LATEST:-false}"

if [[ -z "${REGISTRY}" ]]; then
  cat <<'EOF' >&2
REGISTRY is required.

Example:
  REGISTRY=ghcr.io/your-org IMAGE_TAG=2026-04-14 ./scripts/build-pi-images.sh
EOF
  exit 1
fi

if [[ -z "${IMAGE_TAG}" ]]; then
  IMAGE_TAG="$(date +%Y%m%d-%H%M%S)"
fi

UI_IMAGE="${REGISTRY}/${IMAGE_NAMESPACE}/ui:${IMAGE_TAG}"
API_IMAGE="${REGISTRY}/${IMAGE_NAMESPACE}/api:${IMAGE_TAG}"
UI_LATEST_IMAGE="${REGISTRY}/${IMAGE_NAMESPACE}/ui:latest"
API_LATEST_IMAGE="${REGISTRY}/${IMAGE_NAMESPACE}/api:latest"

echo "Ensuring buildx builder '${BUILDER_NAME}' exists..."
if ! docker buildx inspect "${BUILDER_NAME}" >/dev/null 2>&1; then
  docker buildx create --name "${BUILDER_NAME}" --driver docker-container --use >/dev/null
else
  docker buildx use "${BUILDER_NAME}"
fi

docker buildx inspect --bootstrap >/dev/null

build_and_push() {
  local dockerfile="$1"
  local image="$2"
  local latest_image="$3"
  shift 3
  local extra_args=("$@")

  local tags=(-t "${image}")
  if [[ "${PUSH_LATEST}" == "true" ]]; then
    tags+=(-t "${latest_image}")
  fi

  echo "Building and pushing ${image} (${PLATFORMS})..."
  docker buildx build \
    --platform "${PLATFORMS}" \
    "${tags[@]}" \
    --file "${dockerfile}" \
    --push \
    "${extra_args[@]}" \
    "${ROOT_DIR}"
}

build_and_push "UI/Dockerfile" "${UI_IMAGE}" "${UI_LATEST_IMAGE}" \
  --build-arg VITE_API_BASE_URL="" \
  --build-arg VITE_IS_DEV_MODE="false"

build_and_push "Api/Dockerfile" "${API_IMAGE}" "${API_LATEST_IMAGE}"

cat <<EOF
Done.
Published images:
  ${UI_IMAGE}
  ${API_IMAGE}
EOF

if [[ "${PUSH_LATEST}" == "true" ]]; then
  cat <<EOF
Also tagged:
  ${UI_LATEST_IMAGE}
  ${API_LATEST_IMAGE}
EOF
fi
