// Monaco Editor worker loader
// This file loads the Monaco worker scripts using importScripts
// Use absolute URL from the origin since workers don't support relative paths
const baseUrl = self.location.origin + '/assets/monaco-editor/vs';

self.MonacoEnvironment = {
  baseUrl: baseUrl
};

importScripts(baseUrl + '/base/worker/workerMain.js');
