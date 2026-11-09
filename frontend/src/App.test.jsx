import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import App from './App';

describe('App Component', () => {
  it('renders the main heading', () => {
    render(<App />);
    // Check if the main heading is present
    const headingElement = screen.getByText(/School Admission System/i);
    expect(headingElement).toBeInTheDocument();
  });

  it('renders the Admin and Student buttons', () => {
    render(<App />);
    const adminBtn = screen.getByText(/Admin/i);
    const studentBtn = screen.getByText(/Student/i);
    expect(adminBtn).toBeInTheDocument();
    expect(studentBtn).toBeInTheDocument();
  });
});