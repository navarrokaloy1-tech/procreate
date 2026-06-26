import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VisitForm } from './visit-form';

describe('VisitForm', () => {
  let component: VisitForm;
  let fixture: ComponentFixture<VisitForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [VisitForm],
    }).compileComponents();

    fixture = TestBed.createComponent(VisitForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
