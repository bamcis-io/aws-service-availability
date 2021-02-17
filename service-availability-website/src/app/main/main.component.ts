import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from './../../environments/environment';

@Component({
  selector: 'main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.scss'],
  providers: [
  ]
})
export class MainComponent implements OnInit {
  title = 'AWS Service Availability';

  constructor(private http: HttpClient) {
  }

  ngOnInit(): void {
  }
}
