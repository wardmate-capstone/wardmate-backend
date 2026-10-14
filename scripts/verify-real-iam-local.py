"""Check real local IAM tokens across services. Creates one synthetic local citizen.
Never prints passwords/tokens or calls Azure. Requires APIs on 5001/5003/5004.
"""
import json
import secrets
import time
import urllib.request
import urllib.error
import uuid

checks = 0

def call(port, method, path, body=None, token=None, expected=200):
    global checks
    headers = {'Content-Type': 'application/json', 'X-CSRF-Protection': '1'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(f'http://127.0.0.1:{port}' + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers=headers, method=method)
    try:
        response = urllib.request.urlopen(request, timeout=20)
    except urllib.error.HTTPError as error:
        response = error
    payload = response.read()
    if response.status != expected:
        # Do not include response bodies that may contain credentials.
        raise AssertionError(f'{method} {port}{path}: expected {expected}, got {response.status}')
    checks += 1
    return json.loads(payload) if payload else None

for port in (5001, 5003, 5004):
    for attempt in range(30):
        try:
            call(port, 'GET', '/health')
            break
        except json.JSONDecodeError:
            break  # Plain-text Healthy response.
        except (OSError, AssertionError):
            if attempt == 29:
                raise
            time.sleep(1)

username = 'integration_' + uuid.uuid4().hex[:12]
password = secrets.token_urlsafe(24) + 'Aa1!'
account = call(5001, 'POST', '/api/v1/auth/register', {
    'username': username, 'email': username + '@example.invalid',
    'password': password, 'fullName': 'Local Integration Test'}, expected=201)
auth = call(5001, 'POST', '/api/v1/auth/login', {
    'usernameOrEmail': username, 'password': password})
token = auth['accessToken']
access = call(5001, 'GET', '/api/v1/users/access-context', token=token)
assert 'REGISTERED_CITIZEN' in access['roles']
assert 'workflow.read' in access['permissions']
call(5003, 'GET', '/api/v1/applications', token=token)
call(5004, 'GET', '/api/v1/form-templates', token=token)
call(5004, 'GET', '/api/v1/citizen/submissions?applicantId=' + access['userId'], token=token)
call(5004, 'POST', '/api/v1/form-templates', {'code': 'FORBIDDEN', 'title': 'Forbidden'}, token=token, expected=403)
call(5003, 'GET', '/api/v1/applications', expected=401)
call(5004, 'GET', '/api/v1/form-templates', expected=401)
call(5004, 'GET', '/api/v1/applications/' + str(uuid.uuid4()) + '/contents', token=token, expected=404)
print(f'PASS: {checks} HTTP checks using a real local IAM-issued token.')
print('Synthetic local account retained: ' + username + ' (no real personal data).')
print('No file uploads or Azure requests; full procedure/application flow not covered.')
