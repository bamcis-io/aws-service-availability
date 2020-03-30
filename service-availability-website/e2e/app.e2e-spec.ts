import { SvcAvailabilityWebsitePage } from './app.po';

describe('svc-availability-website App', () => {
  let page: SvcAvailabilityWebsitePage;

  beforeEach(() => {
    page = new SvcAvailabilityWebsitePage();
  });

  it('should display welcome message', () => {
    page.navigateTo();
    expect(page.getParagraphText()).toEqual('Welcome to app!');
  });
});
