SHELL := /bin/sh

DEPLOY_HOST := gfe
DEPLOY_ROOT := /opt/robowar
DEPLOY_SITE := /etc/caddy/sites/robowar.caddy
DEPLOY_URL := https://robowar.afonasev.tech/
BUMP ?= patch
PORT ?= 5173
GIT_BRANCH := $(shell git branch --show-current)
APP_VERSION := $(shell node -p "require('./package.json').version")
BUILD_DATE := $(shell date -u '+%Y-%m-%d %H:%M UTC')

.DEFAULT_GOAL := help
.PHONY: help dev check build deploy

help:
	@printf '%s\n' 'make dev                 Launches a DEV stand labeled with its Git branch.'
	@printf '%s\n' 'make check               Runs typecheck and tests.'
	@printf '%s\n' 'make build               Builds the current version with production metadata.'
	@printf '%s\n' 'make deploy [BUMP=patch] Increments semver, commits the release, builds and publishes main.'

dev:
	VITE_APP_VERSION="$(APP_VERSION)" VITE_GIT_BRANCH="$(GIT_BRANCH)" npm run dev -- --port "$(PORT)"

check:
	npm run typecheck
	npm test

build:
	VITE_APP_VERSION="$(APP_VERSION)" VITE_GIT_BRANCH="$(GIT_BRANCH)" VITE_DEPLOYED_AT="$(BUILD_DATE)" npm run build

deploy:
	@test "$(GIT_BRANCH)" = "main" || { echo 'Deploy must run from main.' >&2; exit 1; }
	@test -z "$$(git status --porcelain)" || { echo 'Commit or stash changes before deploying.' >&2; exit 1; }
	@case "$(BUMP)" in major|minor|patch) ;; *) echo 'BUMP must be major, minor, or patch.' >&2; exit 1;; esac
	npm version "$(BUMP)" --no-git-tag-version
	git add package.json package-lock.json
	git commit -m "chore(release): v$$(node -p \"require('./package.json').version\")"
	$(MAKE) build
	ssh "$(DEPLOY_HOST)" 'install -d -m 755 "$(DEPLOY_ROOT)" /etc/caddy/sites'
	rsync -a --delete dist/ "$(DEPLOY_HOST):$(DEPLOY_ROOT)/"
	scp deploy/robowar.caddy "$(DEPLOY_HOST):$(DEPLOY_SITE)"
	ssh "$(DEPLOY_HOST)" 'caddy validate --config /etc/caddy/Caddyfile && systemctl reload caddy'
	curl --fail --silent --show-error --max-time 20 --retry 3 "$(DEPLOY_URL)" >/dev/null
	@printf '%s\n' "Published v$$(node -p \"require('./package.json').version\") to $(DEPLOY_URL)"
