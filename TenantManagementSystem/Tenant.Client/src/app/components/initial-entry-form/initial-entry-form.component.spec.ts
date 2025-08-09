import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InitialEntryFormComponent } from './initial-entry-form.component';

describe('InitialEntryFormComponent', () => {
  let component: InitialEntryFormComponent;
  let fixture: ComponentFixture<InitialEntryFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InitialEntryFormComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(InitialEntryFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
