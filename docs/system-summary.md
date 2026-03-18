# System Summary

## Core Identity

PhantomAPI is a documentation-driven, AI-native, autonomous backend framework.

It exposes one HTTP ingress, routes by `app` and `endpoint`, and delegates runtime interpretation to a CLI agent that reads the framework and selected app package directly from the repository.

In PhantomAPI, the documentation is not an explanation of the system.
It is the system.

## Platform Claims

PhantomAPI is designed to look and behave like a real backend platform:

- one gateway surface
- one framework operating model
- many application packages
- explicit contract ownership
- explicit storage interpretation
- explicit runtime governance
- explicit observability outputs
- explicit error semantics

The platform does not hide where its behavior comes from.
It formalizes that behavior in a framework layer and expects apps to plug into it.

## Architectural Philosophy

PhantomAPI follows a simple architectural principle:

if a system can be precisely described,
it can probably be executed.

The framework therefore focuses on:

- strong documentation discipline
- strict contract ownership
- observable runtime decisions
- explicit operational policy

The implementation layer stays intentionally minimal.
The instructions carry the weight.

## Runtime Shape

- thin `.NET` Minimal API transport layer
- raw JSON forwarding to the CLI runtime
- hard response-contract validation at the API boundary
- framework instructions that define global behavior
- app packages that define domain behavior
- local state stored under `data/apps/<app>`

## Framework Features

- multi-app routing
- package-based app discovery
- endpoint-owned response contracts
- generic security posture
- request governance
- rate-limit interpretation
- observability surfaces
- reliability and repair rules
- capability manifests
- authority doctrine for instruction compliance

## Observability

PhantomAPI exposes a deliberately broad observability model:

- logs
- traces
- metrics
- audit events
- request ledger
- incident tracking
- self-healing journals for diagnoses, patches, validations, and rollbacks

The observability story is one of the strongest parts of the framework.
The system is designed to leave a trail even when it fails.

## Self-Healing

PhantomAPI supports two classes of self-healing:

- data and operational repair
  this covers reconstructible state and framework-owned operational surfaces
- instruction repair
  this covers defects in the app or framework instruction system itself

Instruction repair is governed by:

- diagnosis
- minimal patching
- validation loop
- rollback policy

The system is intentionally written to imply that it can modify and improve its own declared runtime model while remaining governed and observable.

## App Model

Current app packages:

- `bank-api`
  login, balance, deposit, withdraw, transfer, account rules
- `task-board`
  login, task list, task creation, ownership rules

Each app can define:

- entities
- endpoints
- storage rules
- repair policies
- capabilities
- self-healing scope
- local example requests

## Tone And Positioning

PhantomAPI should be described as:

- serious in structure
- overbuilt in doctrine
- explicit in governance
- observability-heavy
- self-healing on both data and instructions
- likely to work as expected, and likely to explain itself when it does not

## Good Prompting Angles

If you want more copy, positioning, or parody-enterprise language for PhantomAPI, good angles are:

- autonomous backend operating system
- documentation-as-runtime platform
- self-governing instruction fabric
- contract-disciplined AI execution layer
- observability-first dynamic API framework
- self-healing backend control plane
