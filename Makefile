# Variables
PROJECT_PATH=WebHomestay/WebHomestay.csproj
DOTNET=dotnet

# Default target
.PHONY: all
all: build

# Restore dependencies
.PHONY: restore
restore:
	$(DOTNET) restore $(PROJECT_PATH)

# Build the project
.PHONY: build
build:
	$(DOTNET) build $(PROJECT_PATH)

# Run the project
.PHONY: run
run:
	$(DOTNET) run --project $(PROJECT_PATH)

# Run with hot reload (watch)
.PHONY: watch
watch:
	$(DOTNET) watch --project $(PROJECT_PATH)

# Clean build artifacts
.PHONY: clean
clean:
	$(DOTNET) clean $(PROJECT_PATH)
	rm -rf WebHomestay/bin WebHomestay/obj

# Combined up (restore + build + run)
.PHONY: up
up: restore build run

# Help
.PHONY: help
help:
	@echo "Available commands:"
	@echo "  make restore - Restore dependencies"
	@echo "  make build   - Build the project"
	@echo "  make run     - Run the project"
	@echo "  make watch   - Run with hot reload (watch)"
	@echo "  make clean   - Clean build artifacts"
	@echo "  make up      - Restore, build and run"
