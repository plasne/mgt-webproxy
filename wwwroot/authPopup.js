const myMSALObj = new msal.PublicClientApplication(msalConfig);
myMSALObj.acquireTokenPopup(loginRequest).then(
  (response) => {
    document.getElementById("access-token").innerText = response.accessToken;
  },
  (error) => {
    console.error(error);
  }
);
