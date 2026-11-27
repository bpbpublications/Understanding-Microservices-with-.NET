package book.workload
import rego.v1

containers(spec) := array.concat(array.concat(spec.containers, object.get(spec, "initContainers", [])), object.get(spec, "ephemeralContainers", []))
podspec(obj) := obj.spec if obj.kind == "Pod"
podspec(obj) := obj.spec.template.spec if obj.kind in {"Deployment", "StatefulSet", "DaemonSet", "Job"}
unsafe(pod, c) if {
    sc := object.get(c, "securityContext", {})
    parent := object.get(pod, "securityContext", {})
    object.get(sc, "runAsNonRoot", object.get(parent, "runAsNonRoot", false)) != true
}
unsafe(pod, c) if {
    sc := object.get(c, "securityContext", {})
    parent := object.get(pod, "securityContext", {})
    object.get(sc, "runAsUser", object.get(parent, "runAsUser", -1)) == 0
}
deny contains msg if {
    spec := podspec(input)
    some c in containers(spec)
    unsafe(spec, c)
    msg := sprintf("%s must run as a non-root user", [c.name])
}
