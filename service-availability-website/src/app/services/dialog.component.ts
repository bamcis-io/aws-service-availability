import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

@Component({
    template: `
<h2 mat-dialog-title>{{ data.title }}</h2>
<mat-dialog-content>
  {{data.message}}
  <div *ngFor="let arr of data.keyvalues">
    <span style="font-weight:bold;">{{arr.key}}: </span>
    <span>{{arr.value}}</span>
  </div>
</mat-dialog-content>
<mat-dialog-actions>
  <button *ngFor="let button of data.buttons" type="{{button.type}}" mat-dialog-close="{{button.result}}">{{button.text}}</button>
</mat-dialog-actions>
`
})
export class DialogContentComponent {
    constructor(public dialogRef: MatDialogRef<DialogContentComponent>,
        @Inject(MAT_DIALOG_DATA) public data: any) {
    }
}
