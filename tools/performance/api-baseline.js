import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5000';
const email = __ENV.ADMIN_EMAIL;
const password = __ENV.ADMIN_PASSWORD;
const scenario = __ENV.K6_SCENARIO || 'baseline';

const scenarios = {
  smoke: { executor: 'shared-iterations', vus: 1, iterations: 1, maxDuration: '30s' },
  baseline: { executor: 'constant-vus', vus: 10, duration: '60s' },
  load: {
    executor: 'ramping-vus',
    startVUs: 1,
    stages: [
      { duration: '30s', target: 20 },
      { duration: '60s', target: 50 },
      { duration: '30s', target: 0 },
    ],
  },
};

export const options = {
  scenarios: { api: scenarios[scenario] || scenarios.baseline },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{operation:list}': ['p(95)<200'],
    'http_req_duration{operation:login}': ['p(95)<500'],
  },
};

export function setup() {
  if (!email || !password) {
    throw new Error('ADMIN_EMAIL and ADMIN_PASSWORD are required');
  }

  const response = http.post(
    `${baseUrl}/users/login`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' }, tags: { operation: 'login' } },
  );
  check(response, { 'login succeeds': (value) => value.status === 200 });

  const body = response.json();
  const token = body?.data?.access_token || body?.data?.accessToken;
  if (!token) {
    throw new Error('Login response did not contain an access token');
  }

  return { token };
}

export default function (data) {
  const params = {
    headers: { Authorization: `Bearer ${data.token}` },
    tags: { operation: 'list' },
  };

  const responses = http.batch([
    ['GET', `${baseUrl}/warehouses?page=1&pageSize=20`, null, params],
    ['GET', `${baseUrl}/warehouse-documents?page=1&pageSize=20`, null, params],
    ['GET', `${baseUrl}/stock-movements?page=1&pageSize=20`, null, params],
    ['GET', `${baseUrl}/audit-logs?page=1&pageSize=20`, null, params],
    ['GET', `${baseUrl}/assets?page=1&pageSize=20&search=ASSET`, null, params],
  ]);

  check(responses, {
    'all protected reads succeed': (values) => values.every((value) => value.status === 200),
  });
  sleep(0.25);
}
