import { Component, OnInit } from '@angular/core';
import { AuthService } from './services/auth.service';
import { Router } from '@angular/router';
import { environment } from './../environments/environment';
import AWS, { CognitoIdentity } from 'aws-sdk';
import Cognito from 'aws-sdk/clients/cognitoidentityserviceprovider';

@Component({
})
export class AuthCallback implements OnInit {

  constructor(private authService: AuthService, private router: Router) { }

  ngOnInit() {
    console.log("In AuthCallback");

    this.authService.completeAuthentication();
    AWS.config.region = environment.region;
    const options = {
      IdentityPoolId: environment.identityPool,
      Logins: {}
    };
    options.Logins[environment.oidc.name] = this.authService.getIdToken();
    const creds = new AWS.CognitoIdentityCredentials(options);
    creds.expired = true;
    AWS.config.credentials = creds;
    AWS.config.getCredentials(function (err) {
      if (err) {
        console.log(err);
      } else {
        console.log(AWS.config.credentials.accessKeyId);
        console.log(AWS.config.credentials.sessionToken);
      }
    });
    this.router.navigate([""]);
  }
}
