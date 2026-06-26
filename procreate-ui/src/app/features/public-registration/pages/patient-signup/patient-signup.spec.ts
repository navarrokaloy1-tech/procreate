import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PatientSignup } from './patient-signup';

describe('PatientSignup', () => {
  let component: PatientSignup;
  let fixture: ComponentFixture<PatientSignup>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PatientSignup],
    }).compileComponents();

    fixture = TestBed.createComponent(PatientSignup);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
