import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

import { AppComponent } from './app.component';
import { RequestComponent } from './request/request.component';
import { AlertComponent } from './alert/alert.component';
import { ResultComponent } from './result/result.component';
import { LogoutComponent } from './logout/logout.component';
import { AuthCallback } from './auth-callback.component';
import { MainComponent } from './main/main.component';

import { RegionService } from './services/region.service';
import { RequestService } from './services/request.service';
import { AwsServiceService } from './services/aws-service.service';
import { Request } from './request/request';
import { AuthGuardService } from './services/auth-guard.service';
import { AuthService } from './services/auth.service';

import { AppRoutingModule } from './app-routing.module';

import { DialogContentComponent } from './services/dialog.component';

import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { OverlayContainer } from '@angular/cdk/overlay';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { CdkTableModule } from '@angular/cdk/table';
import { MatTableModule } from '@angular/material/table';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSortModule } from '@angular/material/sort';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

@NgModule({
  declarations: [
    AppComponent,
    MainComponent,
    RequestComponent,
    ResultComponent,
    DialogContentComponent,
    AlertComponent,
    LogoutComponent,
    AuthCallback
  ],
  entryComponents: [
    DialogContentComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    BrowserAnimationsModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatTableModule,
    CdkTableModule,
    MatDialogModule,
    MatSortModule,
    MatPaginatorModule,
    MatMenuModule,
    MatTabsModule,
    MatToolbarModule,
    MatCardModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    AppRoutingModule
  ],
  providers: [
    { provide: 'IRegionService', useClass: RegionService },
    { provide: 'IRequestService', useClass: RequestService },
    { provide: 'IAwsServiceService', useClass: AwsServiceService },
    Request,
    AuthGuardService,
    AuthService
  ],
  bootstrap: [AppComponent]
})
export class AppModule {
  constructor(overlayContainer: OverlayContainer) {
    overlayContainer.getContainerElement().classList.add('indigo-pink');
  }
}
