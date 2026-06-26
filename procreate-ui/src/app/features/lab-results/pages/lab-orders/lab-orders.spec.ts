import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LabOrders } from './lab-orders';

describe('LabOrders', () => {
  let component: LabOrders;
  let fixture: ComponentFixture<LabOrders>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [LabOrders],
    }).compileComponents();

    fixture = TestBed.createComponent(LabOrders);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
