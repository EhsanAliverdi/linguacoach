import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { registerCustomFormioComponents } from './app/shared/formio/register-custom-components';

registerCustomFormioComponents();

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
