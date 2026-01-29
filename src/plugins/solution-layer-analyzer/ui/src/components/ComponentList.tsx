import React from 'react';
import { FixedSizeList as List, ListChildComponentProps } from 'react-window';
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
  useApplyScrollbarWidth,
} from '@fluentui/react-components';
import {
  ChevronRightRegular,
  LayerRegular,
} from '@fluentui/react-icons';
import { ComponentResult } from '../types';

const useStyles = makeStyles({
  tableContainer: {
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

interface VirtualizedRowProps extends ListChildComponentProps {
  data: {
    components: ComponentResult[];
    onSelectComponent: (component: ComponentResult) => void;
    selectedComponentId?: string | null;
    styles: ReturnType<typeof useStyles>;
  };
}

const VirtualizedRow: React.FC<VirtualizedRowProps> = ({ index, style, data }) => {
  const { components, onSelectComponent, selectedComponentId, styles } = data;
  const component = components[index];

  return (
    <TableRow 
      aria-rowindex={index + 2}
      key={component.componentId}
      className={styles.clickableRow}
      onClick={() => onSelectComponent(component)}
      style={{
        ...style,
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
        <div>
          <Text weight="semibold">{component.displayName || component.logicalName}</Text>
          {component.displayName && (
            <Text size={200} style={{ display: 'block', color: tokens.colorNeutralForeground3 }}>
              {component.logicalName}
            </Text>
          )}
        </div>
      </TableCell>
      <TableCell>
        <Text>{component.displayName ? '-' : ''}</Text>
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
  );
};

export const ComponentList: React.FC<ComponentListProps> = ({
  components,
  onSelectComponent,
  selectedComponentId,
}) => {
  const styles = useStyles();
  const appliedScrollbarWidthRef = useApplyScrollbarWidth();

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

  const itemData = {
    components,
    onSelectComponent,
    selectedComponentId,
    styles,
  };

  return (
    <div className={styles.tableContainer}>
      <Table
        noNativeElements
        aria-label="Components table"
        aria-rowcount={components.length}
        size="small"
      >
        <TableHeader>
          <TableRow aria-rowindex={1}>
            <TableHeaderCell>Type</TableHeaderCell>
            <TableHeaderCell>Logical Name</TableHeaderCell>
            <TableHeaderCell>Display Name</TableHeaderCell>
            <TableHeaderCell>Table</TableHeaderCell>
            <TableHeaderCell>Layer Sequence</TableHeaderCell>
            <TableHeaderCell>Managed</TableHeaderCell>
            <TableHeaderCell>Publisher</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
            {/* Scrollbar alignment for the header */}
            <div role="presentation" ref={appliedScrollbarWidthRef} />
          </TableRow>
        </TableHeader>
        <TableBody>
          <List
            height={500}
            itemCount={components.length}
            itemSize={70}
            width="100%"
            itemData={itemData}
          >
            {VirtualizedRow}
          </List>
        </TableBody>
      </Table>
    </div>
  );
};
