// AutoDeployer test web resource: <prefix>_/autodeploytest/scripts/test.js
// Edit the marker below and save to trigger an upload + publish.
var AutoDeployTest = AutoDeployTest || {};
AutoDeployTest.marker = "initial";

AutoDeployTest.onLoad = function (executionContext) {
    console.log("AutoDeployTest loaded: " + AutoDeployTest.marker);
};
