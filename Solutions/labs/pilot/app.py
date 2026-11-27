"""AWS Lambda handler for a read-only related-products lookup (Python standard library)."""
RELATED = {"architecture": ["distributed-systems", "observability"], "testing": ["tdd"]}

def handler(event, context):
    import json
    category = (event.get("queryStringParameters") or {}).get("category", "")
    return {"statusCode": 200, "headers": {"content-type": "application/json"},
            "body": json.dumps({"recommendations": RELATED.get(category, [])})}
