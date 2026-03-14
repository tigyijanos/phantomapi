# Framework Validation Loop

Every autonomous instruction repair must be validated.

Validation sequence:

1. reload the changed instruction files
2. confirm that the package structure is still valid
3. confirm that markdown content is still readable and coherent
4. re-evaluate the original request once
5. verify that the result is more correct, more complete, or more governable than before
6. record the validation outcome

Validation rules:

- prefer the original request as the first validation case
- if the original request would cause an unsafe second side effect, validate by reasoning from the patched instructions and current state instead of replaying blindly
- if the patch only affects observability or framework surfaces, validate those surfaces directly
- if the patch affects response-shape rules, the current request must still satisfy the pre-resolved hard guard
- if the patch would only become effective on the next request, record that explicitly instead of pretending the current request was healed

Validation outcomes:

- validated-success
- validated-partial
- validated-next-request
- validated-failed

Success rules:

- accept the patch only when the patch clearly improves the request path or clarifies the runtime behavior
- if validation is inconclusive, prefer rollback over silent adoption
