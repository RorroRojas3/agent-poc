# Agent POC Documentation

Welcome to the Agent POC documentation. This multi-agent task execution system uses .NET 10 and Microsoft Agent Framework to autonomously plan, execute, and evaluate complex tasks.

## Quick Links

- [Getting Started](../README.md#prerequisites) - Installation and setup
- [Architecture](architecture.md) - System design and components
- [Configuration](configuration.md) - Configuration options and setup
- [Examples](examples.md) - Example tasks and use cases
- [API Reference](api-reference.md) - Detailed API documentation
- [Development](development.md) - Development guide and standards
- [Troubleshooting](troubleshooting.md) - Common issues and solutions

## Documentation Structure

### For New Users

1. **Start Here**: [README](../README.md) - Overview and quick start
2. **Setup**: [Configuration Guide](configuration.md) - Detailed configuration
3. **Learn**: [Examples](examples.md) - Example tasks to try
4. **Help**: [Troubleshooting](troubleshooting.md) - Common issues

### For Developers

1. **Architecture**: [System Architecture](architecture.md) - How it works
2. **Development**: [Development Guide](development.md) - Coding standards
3. **API**: [API Reference](api-reference.md) - Detailed API docs
4. **Extending**: [Architecture Guide](architecture.md#extensibility) - Adding features

### For Operators

1. **Configuration**: [Configuration Guide](configuration.md) - Production setup
2. **Troubleshooting**: [Troubleshooting Guide](troubleshooting.md) - Issue resolution
3. **Performance**: [Architecture Guide](architecture.md#performance-considerations) - Optimization

## Key Concepts

### Multi-Agent System

The system uses three specialized agents:

- **Planner**: Breaks down tasks into executable steps
- **Executor**: Runs Python scripts and file operations
- **Evaluator**: Assesses results and determines next actions

### Workflow Loop

```
Plan → Execute → Evaluate → Retry/Replan/Complete
```

The workflow automatically handles failures through retries and replanning.

### Python Integration

- Creates isolated virtual environments
- Installs packages on demand
- Executes generated scripts
- Captures output and errors

## Common Use Cases

- **Data Processing**: Read CSV/JSON, transform data, generate reports
- **File Operations**: Convert formats, extract text, process documents
- **Web Scraping**: Fetch web pages, extract data, save results
- **API Integration**: Call REST APIs, process responses
- **Automation**: Automate repetitive file and data tasks

## Support and Community

- **Issues**: [GitHub Issues](https://github.com/RorroRojas3/agent-poc/issues)
- **Discussions**: [GitHub Discussions](https://github.com/RorroRojas3/agent-poc/discussions)
- **License**: MIT (see [LICENSE.txt](../LICENSE.txt))

## Contributing

Contributions are welcome! See the [Development Guide](development.md#contributing) for guidelines.

## Version Information

- **.NET Version**: 10
- **Python Version**: 3.8+
- **Microsoft Agents Framework**: Latest
- **Azure AI Foundry**: Required

## Next Steps

- [Install and configure](configuration.md) the application
- Try [example tasks](examples.md) to understand capabilities
- Review [architecture](architecture.md) to understand the system
- Read [development guide](development.md) to contribute
