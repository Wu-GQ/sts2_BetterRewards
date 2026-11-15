#!/usr/bin/env bash
# Shared .env loader for RestartCombat bash scripts.

if [[ -n "${MMS_ENV_LOADED:-}" ]]; then
  return 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ENV_FILE="${MMS_ENV_FILE:-${PROJECT_ROOT}/.env}"

if [[ -f "${ENV_FILE}" ]]; then
  had_allexport=0
  if [[ "$-" == *a* ]]; then
    had_allexport=1
  fi

  set -a
  # shellcheck disable=SC1090
  source "${ENV_FILE}"
  if [[ "${had_allexport}" -eq 0 ]]; then
    set +a
  fi
fi

normalize_path() {
  local value="$1"
  if [[ "${value}" =~ ^([A-Za-z]):\\ ]]; then
    local drive_letter path_tail
    drive_letter="$(printf '%s' "${BASH_REMATCH[1]}" | tr '[:upper:]' '[:lower:]')"
    path_tail="${value:2}"
    path_tail="${path_tail//\\//}"
    value="/mnt/${drive_letter}${path_tail}"
  fi
  printf '%s' "${value%/}"
}

if [[ -n "${STS2_INSTALL_DIR:-}" ]]; then
  # Accept either WSL paths (/mnt/c/...) or Windows paths (C:\...).
  STS2_INSTALL_DIR="$(normalize_path "${STS2_INSTALL_DIR}")"
  export STS2_INSTALL_DIR
fi

if [[ -n "${BASELIB_DIR:-}" ]]; then
  BASELIB_DIR="$(normalize_path "${BASELIB_DIR}")"
  export BASELIB_DIR
fi

export MMS_ENV_FILE="${ENV_FILE}"
export MMS_ENV_LOADED=1
