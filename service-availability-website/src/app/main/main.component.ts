import { Component, OnInit } from '@angular/core';

@Component({
  selector: 'main',
  templateUrl: './main.component.html',
  styleUrls: ['./main.component.scss'],
  providers: [
  ]
})
export class MainComponent implements OnInit {
  title = 'AWS Service Availability';

  constructor() {
  }

  ngOnInit(): void {
  }
}
