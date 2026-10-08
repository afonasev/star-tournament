.DEFAULT_GOAL := help

UNITY_TOOLS := ./unity/tools.sh

.PHONY: help check-local check-full check-player prepare test-edit test-play test check check-ui build unity-editor unity-run trooper-help trooper-pipeline trooper-unity-shipping

help: ## Show the supported local Unity workflow.
	@awk 'BEGIN {FS = ":.*##"; printf "Star Tournament (Unity-only)\n\nUsage: make <target>\n\nTargets:\n"} /^[a-zA-Z0-9_-]+:.*##/ {printf "  %-24s %s\n", $$1, $$2}' $(MAKEFILE_LIST)

prepare: ## Regenerate the proving-ground scene after import changes.
	$(UNITY_TOOLS) prepare

test-edit: ## Run Unity EditMode tests.
	$(UNITY_TOOLS) test-edit

test-play: ## Run Unity PlayMode tests.
	$(UNITY_TOOLS) test-play

test: test-edit test-play ## Run both Unity test suites.

check-ui: ## Run the minimal UI-only automatic gate (no natural bot matches).
	python3 tools/check_ui.py

check: check-local ## Run the full Unity test suites without building a Player.

check-full: test ## Run both complete Unity test suites (no Player build).

check-local: check-ui ## Run UI contracts; add tests for the changed behavior.

check-player: check-full
	$(MAKE) build ## Explicit full tests and Development Player build; requires a build request.

build: ## Build the local macOS Development Player (not a distributable release).
	$(UNITY_TOOLS) build

unity-editor: ## Open this project's Unity Editor (do not run alongside tests or Player QA).
	$(UNITY_TOOLS) editor

unity-run: ## Open the already-built local macOS Development Player.
	$(UNITY_TOOLS) run

trooper-help: ## Explain the offline trooper asset pipeline and its prerequisites.
	@sed -n '1,95p' scripts/trooper/README.md

trooper-pipeline: ## Explicitly run the offline authoring pipeline; requires CONFIRM_TROOPER_PIPELINE=1.
	@test "$(CONFIRM_TROOPER_PIPELINE)" = "1" || { echo "Refusing to author asset derivatives. Re-run with CONFIRM_TROOPER_PIPELINE=1 after reading scripts/trooper/README.md." >&2; exit 2; }
	@echo "Running offline trooper authoring; it writes derivatives and evidence, never deploys."
	scripts/trooper/run.sh

trooper-unity-shipping: ## Explicitly create Unity trooper derivatives; requires CONFIRM_TROOPER_UNITY_SHIPPING=1.
	@test "$(CONFIRM_TROOPER_UNITY_SHIPPING)" = "1" || { echo "Refusing to create Unity trooper derivatives. Re-run with CONFIRM_TROOPER_UNITY_SHIPPING=1 after reading scripts/trooper/README.md." >&2; exit 2; }
	@echo "Running offline Unity trooper derivative pipeline; it is not a deploy or distribution command."
	scripts/trooper/unity_shipping.sh
