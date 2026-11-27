package book.workload
import rego.v1

pod := {"kind": "Pod", "spec": {"securityContext": {"runAsNonRoot": true}, "containers": [{"name": "api"}]}}
test_inherited_safe if { reasons := deny with input as pod; count(reasons) == 0 }
test_root_rejected if {
    bad := json.patch(pod, [{"op":"add","path":"/spec/containers/0/securityContext","value":{"runAsUser":0}}])
    reasons := deny with input as bad
    "api must run as a non-root user" in reasons
}
test_unconstrained_rejected if {
    reasons := deny with input as {"kind":"Pod","spec":{"containers":[{"name":"api"}]}}
    count(reasons) == 1
}
test_init_override_rejected if {
    bad := json.patch(pod, [{"op":"add","path":"/spec/initContainers","value":[{"name":"init","securityContext":{"runAsNonRoot":false}}]}])
    reasons := deny with input as bad
    "init must run as a non-root user" in reasons
}
