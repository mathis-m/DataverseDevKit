import React from 'react';
import {
  makeStyles,
  tokens,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Badge,
  Button,
  Text,
  Card,
} from '@fluentui/react-components';
import {
  ChevronRightRegular,
  LayerRegular,
} from '@fluentui/react-icons';
import { ComponentResult } from '../types';

const useStyles = makeStyles({
  tableContainer: {
    maxHeight: '500px',
    overflowY: 'auto',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
  },
  clickableRow: {
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  layerBadges: {
    display: 'flex',
    gap: tokens.spacingHorizontalXXS,
    flexWrap: 'wrap',
  },
  emptyState: {
    padding: tokens.spacingVerticalXXXL,
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
  },
});

interface ComponentListProps {
  components: ComponentResult[];
  onSelectComponent: (component: ComponentResult) => void;
  selectedComponentId?: string | null;
}

export const ComponentList: React.FC<ComponentListProps> = ({
  components,
  onSelectComponent,
  selectedComponentId,
}) => {
  const styles = useStyles();

  if (components.length === 0) {
    return (
      <Card>
        <div className={styles.emptyState}>
          <LayerRegular style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalM }} />
          <Text size={500}>No components found</Text>
          <Text size={300}>Try adjusting your filters or index more solutions</Text>
        </div>
      </Card>
    );
  }

  return (
    <div className={styles.tableContainer}>
      <Table size="small">
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Type</TableHeaderCell>
            <TableHeaderCell>Logical Name</TableHeaderCell>
            <TableHeaderCell>Display Name</TableHeaderCell>
            <TableHeaderCell>Table</TableHeaderCell>
            <TableHeaderCell>Layer Sequence</TableHeaderCell>
            <TableHeaderCell>Managed</TableHeaderCell>
            <TableHeaderCell>Publisher</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {components.map((component) => (
            <TableRow 
              key={component.componentId}
              className={styles.clickableRow}
              onClick={() => onSelectComponent(component)}
              style={{
                backgroundColor: selectedComponentId === component.componentId 
                  ? tokens.colorNeutralBackground1Selected 
                  : undefined,
              }}
            >
              <TableCell>
                <Badge appearance="outline" color="informative">
                  {component.componentType}
                </Badge>
              </TableCell>
              <TableCell>
                <Text weight="semibold">{component.logicalName}</Text>
              </TableCell>
              <TableCell>
                <Text>{component.displayName || '-'}</Text>
              </TableCell>
              <TableCell>
                <Text size={200}>{component.tableLogicalName || '-'}</Text>
              </TableCell>
              <TableCell>
                <div className={styles.layerBadges}>
                  {component.layerSequence.map((solution, i) => (
                    <Badge 
                      key={i} 
                      appearance="filled"
                      color={i === 0 ? 'success' : i === component.layerSequence.length - 1 ? 'danger' : 'warning'}
                      size="small"
                    >
                      {i + 1}. {solution}
                    </Badge>
                  ))}
                </div>
              </TableCell>
              <TableCell>
                <Badge appearance={component.isManaged ? 'filled' : 'outline'}>
                  {component.isManaged ? 'Yes' : 'No'}
                </Badge>
              </TableCell>
              <TableCell>
                <Text size={200}>{component.publisher || '-'}</Text>
              </TableCell>
              <TableCell>
                <Button
                  size="small"
                  appearance="subtle"
                  icon={<ChevronRightRegular />}
                  onClick={(e) => {
                    e.stopPropagation();
                    onSelectComponent(component);
                  }}
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
};
