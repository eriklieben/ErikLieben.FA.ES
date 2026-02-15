#!/bin/bash

# ErikLieben.FA.ES Benchmark Runner
# Usage: ./run-benchmarks.sh [options]
#
# Options:
#   --all             Run all benchmarks
#   --filter PATTERN  Filter benchmarks by pattern (e.g., "*Json*")
#   --category CAT    Run by category: Core, Serialization, Folding, Registry, Upcasting, Parsing, Storage
#   --list            List available benchmarks
#   --quick           Run with reduced iterations (development mode)
#   --output DIR      Custom output directory
#   --help            Show this help message

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BENCHMARK_PROJECT="$SCRIPT_DIR/ErikLieben.FA.ES.Benchmarks"

# Parse arguments
FILTER=""
ALL=false
CATEGORY=""
LIST=false
QUICK=false
OUTPUT_DIR=""

show_help() {
    echo "ErikLieben.FA.ES Benchmark Runner"
    echo "=================================="
    echo ""
    echo "Usage: ./run-benchmarks.sh [options]"
    echo ""
    echo "Options:"
    echo "  --all             Run all benchmarks"
    echo "  --filter PATTERN  Filter benchmarks by pattern (e.g., \"*Json*\")"
    echo "  --category CAT    Run by category: Core, Serialization, Folding, Registry, Upcasting, Parsing, Storage"
    echo "  --list            List available benchmarks"
    echo "  --quick           Run with reduced iterations (development mode)"
    echo "  --output DIR      Custom output directory"
    echo "  --help            Show this help message"
    echo ""
    echo "Examples:"
    echo "  ./run-benchmarks.sh --all                    # Run all benchmarks"
    echo "  ./run-benchmarks.sh --filter \"*Json*\"        # Run JSON benchmarks"
    echo "  ./run-benchmarks.sh --category Serialization # Run serialization category"
    echo "  ./run-benchmarks.sh --quick --filter \"*Session*\" # Quick run of session benchmarks"
}

while [[ $# -gt 0 ]]; do
    case $1 in
        --all)
            ALL=true
            shift
            ;;
        --filter)
            FILTER="$2"
            shift 2
            ;;
        --category)
            CATEGORY="$2"
            shift 2
            ;;
        --list)
            LIST=true
            shift
            ;;
        --quick)
            QUICK=true
            shift
            ;;
        --output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Build arguments
ARGS=()

if [ "$LIST" = true ]; then
    ARGS+=("--list" "flat")
elif [ "$ALL" = true ]; then
    ARGS+=("--all")
elif [ -n "$CATEGORY" ]; then
    # Map category to filter pattern
    case $CATEGORY in
        Core)
            ARGS+=("--filter" "*EventStream*,*Session*")
            ;;
        Serialization)
            ARGS+=("--filter" "*Json*")
            ;;
        Folding)
            ARGS+=("--filter" "*Fold*")
            ;;
        Registry)
            ARGS+=("--filter" "*Registry*")
            ;;
        Upcasting)
            ARGS+=("--filter" "*Upcaster*")
            ;;
        Parsing)
            ARGS+=("--filter" "*Token*")
            ;;
        Storage)
            ARGS+=("--filter" "*DataStore*")
            ;;
        *)
            echo "Unknown category: $CATEGORY"
            exit 1
            ;;
    esac
elif [ -n "$FILTER" ]; then
    ARGS+=("--filter" "$FILTER")
fi

if [ "$QUICK" = true ] && [ "$LIST" = false ]; then
    ARGS+=("--job" "short")
fi

if [ -n "$OUTPUT_DIR" ]; then
    ARGS+=("--artifacts" "$OUTPUT_DIR")
fi

# Display configuration
echo "ErikLieben.FA.ES Benchmark Runner"
echo "=================================="
echo ""
echo "Configuration:"
echo "  Project: $BENCHMARK_PROJECT"
if [ "$QUICK" = true ]; then
    echo "  Mode: Quick (ShortRun)"
else
    echo "  Mode: Full benchmark"
fi
[ -n "$FILTER" ] && echo "  Filter: $FILTER"
[ -n "$CATEGORY" ] && echo "  Category: $CATEGORY"
[ "$ALL" = true ] && echo "  Running: All benchmarks"
echo ""

# Run benchmarks
cd "$BENCHMARK_PROJECT"
echo "Running: dotnet run -c Release -- ${ARGS[*]}"
echo ""

dotnet run -c Release -- "${ARGS[@]}"

if [ $? -eq 0 ] && [ "$LIST" = false ]; then
    echo ""
    echo "Benchmark completed successfully!"
    echo ""
    echo "Results saved to:"
    ARTIFACTS_DIR="${OUTPUT_DIR:-BenchmarkDotNet.Artifacts/results}"
    echo "  HTML: $ARTIFACTS_DIR/*.html"
    echo "  CSV:  $ARTIFACTS_DIR/*.csv"
    echo "  MD:   $ARTIFACTS_DIR/*-github.md"
fi
