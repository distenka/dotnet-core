# Overview

Build in a couple of hours & reduce cost/time for your current/next feature

![Logo](https://avatars.githubusercontent.com/u/191408113?s=200&v=4)

**Distenka Platform** Is scalable and secured serverless computing platform for **AI** and **vibe coding**.


## Features at a Glance

* Vibe coding assistant in your first processor
* Single or multi-cluster setup.
* Automated failover to backup servers.
* Auto-discovery of nearby servers.
* Real-time process status with live log viewer.
* Processor can be written in any language **Currently .Net**.
* Schedule events in multiple time zones.
* Track CPU and memory usage for each process.
* Historical stats with performance graphs.
* Simple JSON messaging system for Processors.
* Web hooks for external notification systems.
* Simple REST API for scheduling and running events.
* API Keys for authenticating remote apps.

## Documentation

Documentation coming soon on:

- &rarr; **[Installation & Setup](https://distenka.ai/docs)**
- &rarr; **[Configuration](https://distenka.ai/docs)**
- &rarr; **[Web UI](https://distenka.ai/docs)**
- &rarr; **[Processors](https://distenka.ai/docs)**
- &rarr; **[Command Line](https://distenka.ai/docs)**
- &rarr; **[Inner Workings](https://distenka.ai/docs)**
- &rarr; **[API Reference](https://distenka.ai/docs)**
- &rarr; **[Development](https://distenka.ai/docs)**



## 🚀: Get Started

Check `/src/Templates/samples/` **SubscriptionServiceProcessor** example processor
- ```dotnet add package Distenka --version 1.0.0```

- It includes processes for managing subscriptions and bank accounts, such as creating, modifying, activating, and deactivating subscriptions, as well as adding and validating bank accounts.

### Prerequisites
- .NET 6.0 or later
- SQL Server (or your database of choice)
- Configure the database connection in appsettings.json or via environment variables

### Setting Up the Database
- Update the connection string in appsettings.json or set it via environment variables.
- Example (appsettings.json):
json

```
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=dbName;Trusted_Connection=True;"
  }
}
```

### Running Processes
The project uses the Distenka framework to manage and execute processes. Below are the key CLI commands for interacting with the processes.

To see all registered Processes in the application, run:

```
dotnet run -- list
```

This will display a list of process names, such as `SubscriptionServiceProcessor.CreateSubscriptionProcess`, etc.

To see what the config file looks like, simply generate a JSON file for the process. run

```
dotnet run -- get "SubscriptionServiceProcessor.CreateSubscriptionProcess" CreateSubscriptionProcess.json
```
Here is the output file

```
{
  "userId": 0,
  "planId": 0,
  "process": "SubscriptionServiceProcessor.CreateSubscriptionProcess"
}
```

to run process locally 

```
dotnet run -- run "CreateSubscriptionProcess.json" "createSubscriptionProcess-results.json"
```
    
