const http = require('http');
http.createServer((req, res) => {
  res.writeHead(200, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ count: 1, value: [{ id: 1, name: 'MockVariableGroup', variables: { 'DB_PASS': { value: 'secret' } } }] }));
}).listen(8081, () => console.log('Mock ADO server running on 8081'));
