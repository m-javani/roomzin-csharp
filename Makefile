.PHONY: clean build push all

clean:
	dotnet clean

build: clean
	dotnet build
	dotnet pack -c Release

push:
	@if [ -z "$(VERSION)" ]; then \
		echo "Error: VERSION not set. Run: make push VERSION=1.0.2 API_KEY=your-key"; \
		exit 1; \
	fi
	@if [ -z "$(API_KEY)" ]; then \
		echo "Error: API_KEY not set. Run: make push VERSION=1.0.2 API_KEY=your-key"; \
		exit 1; \
	fi
	dotnet nuget push src/Roomzin.Sdk/bin/Release/Roomzin.Sdk.$(VERSION).nupkg -k $(API_KEY) -s https://api.nuget.org/v3/index.json

all: build push