import { Injectable } from '@angular/core';
import { UserManager, User, UserManagerSettings } from 'oidc-client';
import { environment } from './../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private user: User = null;
  private manager = new UserManager(this.getClientSettings());

  constructor() {
    this.manager.getUser().then(user => {
      this.user = user;
    });
  }

  isLoggedIn(): boolean {
    return this.user != null && !this.user.expired;
  }

  getClaims(): any {
    return this.user.profile;
  }

  getAuthorizationHeaderValue(): string {
    return `${this.user.token_type} ${this.user.access_token}`;
  }

  startAuthentication(): Promise<void> {
    return this.manager.signinRedirect();
  }

  completeAuthentication(): Promise<void> {
    return this.manager.signinRedirectCallback().then(user => {
      this.user = user;
    });
  }

  getIdToken(): string {
    return this.user.id_token;
  }

  logout(): void {
    this.manager.removeUser();
  }

  private getClientSettings(): UserManagerSettings {
    return {
      authority: environment.oidc.authority,
      client_id: environment.oidc.client_id,
      redirect_uri: environment.oidc.redirect_uri,
      post_logout_redirect_uri: environment.oidc.logout_redirect_uri,
      response_type: "id_token",
      scope: "openid",
      filterProtocolClaims: true,
      loadUserInfo: true
    };
  }
}
