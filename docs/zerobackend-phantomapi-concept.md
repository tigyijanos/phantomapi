# ZeroBackend / PhantomAPI

### AI‑Native Backend Architecture (Experimental / Satirical Concept)

## Overview

This document describes an experimental and partly satirical project
idea inspired by the current hype around AI agents.

The core idea is intentionally exaggerated:

Instead of writing backend code, the entire backend behavior is defined
through **instruction files** that an AI agent interprets at runtime.

This means:

-   No controllers
-   No services
-   No repositories
-   No business logic implemented in code

The backend becomes an **AI runtime that interprets declarative
instructions**.

The project is partly serious experimentation and partly a parody of the
idea that AI agents will soon replace most backend engineering work.

------------------------------------------------------------------------

# Project Naming

Several possible names fit the concept.

## ZeroBackend

**Tagline**

> The last backend you'll ever need.

Alternative:

> Finally: a backend without backend engineers.

Description:

ZeroBackend is an experimental architecture where backend code no longer
exists.\
Instead of writing controllers, services, and repositories, developers
define the system through instruction files and an autonomous AI agent
interprets them at runtime.

------------------------------------------------------------------------

## PhantomAPI

**Tagline**

> The backend that doesn't exist.

Alternative:

> An API powered entirely by vibes and instructions.

Description:

PhantomAPI is a backend architecture where the backend technically does
not exist.\
The entire system is interpreted dynamically by an AI agent reading
instruction files.

------------------------------------------------------------------------

# Core Idea

Traditional backend architecture requires engineers to implement
business logic in code.

Dynamic agent‑based architecture proposes a different approach.

All business logic is defined in **instruction files**, and an AI agent
decides how to execute the logic dynamically.

Responsibilities of the agent:

-   interpret business rules
-   decide what database queries to run
-   generate responses
-   perform side effects
-   write logs
-   maintain traces

Instead of implementing logic in code, we simply **describe the
system**.

------------------------------------------------------------------------

# Architecture

## Traditional Backend

    Client
       |
    Controller
       |
    Service
       |
    Repository
       |
    Database

## Dynamic Agent Backend

    Client
       |
    POST /dynamic-api
       |
    Agent Runtime
       |
    Instruction Files
       |
    Database / Filesystem / Tools

The system exposes only **one API endpoint**.

The agent decides everything else.

------------------------------------------------------------------------

# Example Request Flow

Example client request:

``` json
{
  "action": "withdraw",
  "userId": 42,
  "amount": 100
}
```

Agent runtime process:

1.  Reads instruction files
2.  Determines relevant business rules
3.  Queries the database
4.  Executes required actions
5.  Generates the response

------------------------------------------------------------------------

# Repository Structure

Example repository layout:

    zero-backend/

    instructions/
        entities.md
        business-rules.md
        security.md
        payment-flow.md

    prompts/
        agent-system.md
        decision-framework.md

    runtime/
        agent-runner.ts
        db-tool.ts
        file-tool.ts

    api/
        server.ts

    examples/
        ecommerce/
        banking/

    README.md

Important principle:

**Backend logic lives in instruction files, not in code.**

------------------------------------------------------------------------

# Example Instruction Files

## entities.md

    Entity: User
    Fields:
    - id
    - email
    - balance

    Entity: Payment
    Fields:
    - id
    - userId
    - amount

------------------------------------------------------------------------

## business-rules.md

    Rule 1:
    Users can deposit money.

    Rule 2:
    Users cannot withdraw more than their balance.

    Rule 3:
    All transactions must be logged.

------------------------------------------------------------------------

# Agent System Prompt

Example configuration:

    You are the backend runtime.

    Your job is to interpret instruction files and decide how the backend should behave.

    You may:
    - query the database
    - update records
    - write files
    - generate responses
    - log events

    Follow the business rules defined in the instruction files.

------------------------------------------------------------------------

# API Design

The entire system exposes a single endpoint.

    POST /dynamic-api

The request is interpreted dynamically by the agent.

------------------------------------------------------------------------

# Possible Features (Hype Mode)

To fully embrace the AI‑agent hype, the system could include features
such as:

### Autonomous Business Logic

The backend decides how to execute business rules dynamically.

### Prompt Driven Development

New features are added by updating instruction files.

### Self Healing Backend

If something breaks, the agent modifies the instructions.

### AI Governance Layer

The agent reviews its own decisions.

### Compliance Engine

The agent evaluates whether operations comply with policies such as
GDPR.

### Natural Language Database Queries

The agent decides which queries to run.

------------------------------------------------------------------------

# Vision

The long‑term vision of this architecture is intentionally exaggerated.

> Backend engineers will no longer write backend code.\
> They will maintain instruction files that AI agents interpret.

------------------------------------------------------------------------

# Purpose of the Project

This project exists to:

1.  Explore the limits of agent‑driven systems
2.  Demonstrate potential problems with AI‑driven architectures
3.  Satirically reflect on current AI hype
4.  Experiment with alternative backend development models

------------------------------------------------------------------------

# Disclaimer

This project is experimental.

It should **not be used in production under any circumstances**.

Unless the agent decides otherwise.
