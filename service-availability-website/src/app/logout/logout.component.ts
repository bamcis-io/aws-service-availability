import { Component, OnInit } from '@angular/core';
import { AuthService } from './../services/auth.service';

@Component({
  selector: 'app-root',
  templateUrl: './logout.component.html',
  styleUrls: ['./logout.component.scss'],
  providers: [
  ]
})
export class LogoutComponent implements OnInit {
  title = 'AWS Service Availability';
  //logout = "You have been logged out";

  constructor(private authService: AuthService) { }

  ngOnInit() {
    this.authService.logout();
  }
}
